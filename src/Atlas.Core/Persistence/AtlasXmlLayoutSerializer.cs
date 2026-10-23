using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Atlas.Core.Diagnostics;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Persistence;

/// <summary>
///     Implements the strict Atlas V2 XML persistence contract: snapshot domain
///     invariants are validated before writing; on read the root shape dispatches
///     V1/V2 first (including V1 migration), then schema validation, resource limits
///     and domain invariant validation run in order, with every failure normalized
///     into a diagnostic error.
/// </summary>
public sealed partial class AtlasXmlLayoutSerializer : IAtlasLayoutSerializer
{
    // V2 文档的 XML 命名空间常量。
    private const string NamespaceValue = "urn:atlas:workspace:2";

    // 命名空间的 XNamespace 实例；构造与读取 XML 元素共用。
    private static readonly XNamespace Namespace = NamespaceValue;

    // 惰性加载的 XSD 校验集；读取路径首次执行架构校验时才构建。
    private static readonly Lazy<XmlSchemaSet> Schemas = new(CreateSchemas);

    /// <inheritdoc />
    public string Serialize(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureSnapshotValid(snapshot);

        var root = new XElement(
            Namespace + "AtlasWorkspace",
            new XAttribute("schemaVersion", LayoutSchemaVersion.Current),
            new XAttribute("workspaceId", snapshot.Id.Value),
            WriteItems(snapshot),
            WriteLayout(snapshot),
            WriteToolBars(snapshot),
            WriteAutoHide(snapshot));
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        EnsureSnapshotWithinLimits(snapshot, document);
        using var writer = new Utf8StringWriter(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.None);
        var xml = writer.ToString();
        EnsureDocumentWithinLimit(xml);
        return xml;
    }

    /// <inheritdoc />
    public WorkspaceResult<LayoutRestoreDocument> Deserialize(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutPersistenceLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        if (targetWorkspaceId == default)
        {
            throw new ArgumentException(
                "A target workspace identifier cannot be default.",
                nameof(targetWorkspaceId));
        }

        // 在进入 XML 解析前先按字节上限拒绝超大输入，避免对不可信文档投入解析成本。
        limits ??= new LayoutPersistenceLimits();
        if (Encoding.UTF8.GetByteCount(xml) > limits.MaxDocumentBytes)
        {
            return Invalid(WorkspaceDiagnostics.Ids.PersistLimit);
        }

        try
        {
            var document = LoadSecure(xml, limits);
            var root = document.Root;
            if (root is null)
            {
                return Invalid(WorkspaceDiagnostics.Ids.PersistRoot);
            }

            var version = (string?)root.Attribute("schemaVersion");
            // 先按根元素形状分派版本：无命名空间的 AtlasLayout 是 V1 文档，走迁移路径；
            // 其余必须精确匹配 V2 命名空间与根名，否则视为不支持的版本。
            if (root.Name.NamespaceName.Length == 0
                && root.Name.LocalName == "AtlasLayout")
            {
                return ReadLegacy(root, targetWorkspaceId, limits);
            }

            if (root.Name.NamespaceName != NamespaceValue
                || root.Name.LocalName != "AtlasWorkspace")
            {
                return InvalidVersion();
            }

            // 版本兼容按主版本判定：任何 2.x 都视为可读，为同主版本的格式演进留出空间。
            if (!TryCompatibleVersion(version))
            {
                return InvalidVersion();
            }

            // 先按嵌入式 XSD 校验结构，再做规模限制扫描，两者都通过才进入对象构建。
            ValidateSchema(document);
            ValidateLimits(root, limits);
            // 恢复文档必须属于目标工作区，防止把其他工作区的布局灌入当前工作区。
            var persistedWorkspaceId = Required(root, "workspaceId");
            if (!StringComparer.Ordinal.Equals(persistedWorkspaceId, targetWorkspaceId.Value))
            {
                return Invalid(WorkspaceDiagnostics.Ids.PersistWorkspace);
            }

            // XML 合法不等于语义合法：快照仍须通过域不变量校验，失败即整体拒绝恢复。
            var snapshot = ReadSnapshot(root, targetWorkspaceId, limits);
            var violations = WorkspaceInvariantValidator.Validate(snapshot);
            if (violations.Count != 0)
            {
                return Invalid(WorkspaceDiagnostics.Ids.PersistInvariant);
            }

            return WorkspaceResult<LayoutRestoreDocument>.Success(
                new LayoutRestoreDocument(snapshot, version!));
        }
        catch (PersistenceLimitException)
        {
            return Invalid(WorkspaceDiagnostics.Ids.PersistLimit);
        }
        catch (XmlSchemaValidationException)
        {
            return Invalid(WorkspaceDiagnostics.Ids.PersistSchema);
        }
        catch (Exception exception) when (
            exception is XmlException
            or InvalidOperationException
            or ArgumentException
            or FormatException
            or OverflowException)
        {
            // 把剩余解析/校验结构错误归一为“文档无效”诊断，调用方只需处理预期失败。
            return Invalid(WorkspaceDiagnostics.Ids.PersistenceInvalid);
        }
    }

    /// <inheritdoc />
    public WorkspaceResult<LayoutRestoreDocument> DeserializeWithOptions(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutRestoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var parsed = Deserialize(xml, targetWorkspaceId, options.Limits);
        if (!parsed.Succeeded)
        {
            return parsed;
        }

        return ApplyRestoreOptions(parsed.Value!, options);
    }

    private static bool ParseOptionalBool(XElement element, string name)
    {
        return Optional(element, name) is { } value && ParseBool(value);
    }

    private static void RequireAllowedAttributes(XElement element, params string[] names)
    {
        var allowed = names.ToHashSet(StringComparer.Ordinal);
        if (element.Attributes().Any(
            attribute => attribute.IsNamespaceDeclaration
                || !allowed.Contains(attribute.Name.LocalName)
                || attribute.Name.NamespaceName.Length != 0))
        {
            throw new XmlException("The legacy input contains an unknown attribute.");
        }
    }

    private static void EnsureNoChildren(XElement element)
    {
        if (element.Elements().Any())
        {
            throw new XmlException("The legacy input contains an unknown element.");
        }
    }

}
