namespace Atlas.Core.Items;

/// <summary>
///     工作区中的不可变工具条目或文档条目；构造函数校验种类、能力集与文档
///     状态的组合，之后所有属性均为只读。
/// </summary>
public sealed class DockItem
{
    /// <summary>
    ///     构造不可变条目并校验组合约束：Id 与 Content 不得为默认值，kind 仅限
    ///     tool/document；Capabilities 必须是该种类允许的子集（工具条目不能分割
    ///     或固定，文档条目不能自动隐藏）；工具条目不得携带 DocumentState，固定
    ///     文档必须声明 CanPinDocument，预览文档则不能声明。
    /// </summary>
    /// <param name="id">工作区范围内的条目标识符。</param>
    /// <param name="kind">条目的行为种类。</param>
    /// <param name="content">内容渲染器与资源标识符。</param>
    /// <param name="capabilities">条目的稳定能力集；必须与种类匹配。</param>
    /// <param name="title">显示标题；为 <see langword="null"/> 时使用条目标识符。</param>
    /// <param name="iconKey">可选的、与 UI 无关的图标标识符。</param>
    /// <param name="documentState">仅由文档提交的状态；工具条目传入非 <see langword="null"/> 值时会抛出异常。</param>
    public DockItem(
        DockItemId id,
        DockItemKind kind,
        ContentReference content,
        DockItemCapabilities capabilities,
        string? title = null,
        string? iconKey = null,
        DocumentState? documentState = null)
    {
        if (id == default)
        {
            throw new ArgumentException("A dock item identifier cannot be the default value.", nameof(id));
        }

        if (content.IsDefault)
        {
            throw new ArgumentException("A content reference cannot be the default value.", nameof(content));
        }

        if (kind is not DockItemKind.Tool and not DockItemKind.Document)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown dock item kind.");
        }

        ValidateCapabilities(kind, capabilities);
        ValidatePresentation(title, iconKey);

        var resolvedDocumentState = ResolveDocumentState(kind, capabilities, documentState);

        Id = id;
        Kind = kind;
        Content = content;
        Capabilities = capabilities;
        Title = title ?? id.Value;
        IconKey = iconKey;
        DocumentState = resolvedDocumentState;
    }

    /// <summary>
    ///     工作区范围内的条目标识符。
    /// </summary>
    public DockItemId Id { get; }

    /// <summary>
    ///     条目的行为种类：决定承载它的 Group 与允许的能力集。
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    ///     显示标题；构造时未提供则回退到 <see cref="Id"/> 的字符串值。
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     可选的、与 UI 无关的图标标识符；<see langword="null"/> 表示无图标。
    /// </summary>
    public string? IconKey { get; }

    /// <summary>
    ///     内容引用：渲染器种类与资源标识符，也是内容身份比较与去重决策的基础。
    /// </summary>
    public ContentReference Content { get; }

    /// <summary>
    ///     条目的稳定能力集：声明条目支持的用户操作，并在构造时按种类校验。
    /// </summary>
    public DockItemCapabilities Capabilities { get; }

    /// <summary>
    ///     文档条目的固定/预览状态；工具条目始终为 <see langword="null"/>。
    /// </summary>
    public DocumentState? DocumentState { get; }

    private static void ValidateCapabilities(
        DockItemKind kind,
        DockItemCapabilities capabilities)
    {
        if ((capabilities & ~DockItemCapabilityPresets.KnownCapabilities) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capabilities),
                capabilities,
                "The capability set contains unknown flags.");
        }

        if ((capabilities & ~DockItemCapabilityPresets.AllowedFor(kind)) != 0)
        {
            throw new ArgumentException(
                $"The capability set is not valid for a {kind} item.",
                nameof(capabilities));
        }
    }

    private static void ValidatePresentation(string? title, string? iconKey)
    {
        if (title is not null && string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "A specified title cannot be empty or whitespace.",
                nameof(title));
        }

        if (iconKey is not null && string.IsNullOrWhiteSpace(iconKey))
        {
            throw new ArgumentException(
                "A specified icon key cannot be empty or whitespace.",
                nameof(iconKey));
        }
    }

    private static DocumentState? ResolveDocumentState(
        DockItemKind kind,
        DockItemCapabilities capabilities,
        DocumentState? documentState)
    {
        if (kind == DockItemKind.Tool)
        {
            if (documentState is not null)
            {
                throw new ArgumentException(
                    "Tool items cannot contain document state.",
                    nameof(documentState));
            }

            return null;
        }

        var resolvedState = documentState ?? default;

        if (resolvedState.IsPinned
            && !capabilities.HasFlag(DockItemCapabilities.CanPinDocument))
        {
            throw new ArgumentException(
                "A pinned document must support the pin capability.",
                nameof(documentState));
        }

        if (resolvedState.IsPreview
            && capabilities.HasFlag(DockItemCapabilities.CanPinDocument))
        {
            throw new ArgumentException(
                "A preview document cannot support the pin capability.",
                nameof(capabilities));
        }

        return resolvedState;
    }
}
