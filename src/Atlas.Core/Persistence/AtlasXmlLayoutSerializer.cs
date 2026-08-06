using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Persistence;

/// <summary>Implements the strict Atlas V2 XML persistence contract.</summary>
public sealed partial class AtlasXmlLayoutSerializer : IAtlasLayoutSerializer
{
    private const string NamespaceValue = "urn:atlas:workspace:2";
    private const string CurrentVersion = "2.0";
    private static readonly XNamespace Namespace = NamespaceValue;
    private static readonly Lazy<XmlSchemaSet> Schemas = new(CreateSchemas);

    /// <inheritdoc />
    public string Serialize(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureSnapshotValid(snapshot);

        var root = new XElement(
            Namespace + "AtlasWorkspace",
            new XAttribute("schemaVersion", CurrentVersion),
            new XAttribute("workspaceId", snapshot.Id.Value),
            WriteItems(snapshot),
            WriteLayout(snapshot),
            WriteToolBars(snapshot),
            WriteAutoHide(snapshot));
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        using var writer = new Utf8StringWriter(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.None);
        return writer.ToString();
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

        limits ??= new LayoutPersistenceLimits();
        if (Encoding.UTF8.GetByteCount(xml) > limits.MaxDocumentBytes)
        {
            return Invalid("ATLAS-PERSIST-LIMIT");
        }

        try
        {
            var document = LoadSecure(xml, limits);
            var root = document.Root;
            if (root is null)
            {
                return Invalid("ATLAS-PERSIST-ROOT");
            }

            var version = (string?)root.Attribute("schemaVersion");
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

            if (!TryCompatibleVersion(version))
            {
                return InvalidVersion();
            }

            ValidateSchema(document);
            ValidateLimits(root, limits);
            var persistedWorkspaceId = Required(root, "workspaceId");
            if (!StringComparer.Ordinal.Equals(persistedWorkspaceId, targetWorkspaceId.Value))
            {
                return Invalid("ATLAS-PERSIST-WORKSPACE");
            }

            var snapshot = ReadSnapshot(root, targetWorkspaceId, limits);
            var violations = WorkspaceInvariantValidator.Validate(snapshot);
            if (violations.Count != 0)
            {
                return Invalid("ATLAS-PERSIST-INVARIANT");
            }

            return WorkspaceResult<LayoutRestoreDocument>.Success(
                new LayoutRestoreDocument(snapshot, version!));
        }
        catch (Exception exception) when (
            exception is XmlException
            or XmlSchemaValidationException
            or InvalidOperationException
            or ArgumentException
            or FormatException
            or OverflowException)
        {
            return Invalid("ATLAS-PERSIST-INVALID");
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

    private sealed record LegacyPanel(
        DockItemId Id,
        LogicalRegion Region,
        bool Active,
        bool AutoHidden,
        double? Size) : ILegacyItem;

    private sealed record LegacyEditor(
        DockItemId Id,
        bool Active,
        bool Pinned) : ILegacyItem;
}
