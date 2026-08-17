namespace Atlas.Core.Items;

/// <summary>
///     工作区中不可变的工具项或文档项；
///     构造时校验种类、能力集与文档状态的组合一致性，之后所有属性不可再变。
/// </summary>
public sealed class DockItem
{
    // 全部已知能力的并集；构造时据此拒绝未声明的能力位。
    private const DockItemCapabilities KnownCapabilities =
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide
        | DockItemCapabilities.CanSplit
        | DockItemCapabilities.CanPinDocument;

    // 工具项允许的能力子集：可关闭/移动/自动隐藏，不含拆分与固定文档。
    private const DockItemCapabilities ToolCapabilities =
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    // 文档项允许的能力子集：可关闭/移动/拆分/固定文档，不含自动隐藏。
    private const DockItemCapabilities DocumentCapabilities =
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanSplit
        | DockItemCapabilities.CanPinDocument;

    /// <summary>
    ///     构造不可变项并校验组合约束：Id 与 Content 不得为默认值，kind 仅限工具/文档；
    ///     Capabilities 必须属于该 kind 允许的能力子集（工具项不可拆分/固定，文档项不可自动隐藏），
    ///     工具项禁止携带 DocumentState，固定文档必须声明 CanPinDocument，预览文档不得声明。
    /// </summary>
    /// <param name="id">工作区作用域内的项标识。</param>
    /// <param name="kind">项的行为种类。</param>
    /// <param name="content">内容渲染器与资源标识。</param>
    /// <param name="capabilities">项的稳定能力集；必须与 kind 匹配。</param>
    /// <param name="title">显示标题；为 <see langword="null"/> 时使用项标识。</param>
    /// <param name="iconKey">可选的与 UI 无关的图标标识。</param>
    /// <param name="documentState">仅文档提交的状态；工具项传非 <see langword="null"/> 抛异常。</param>
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
    ///     工作区作用域内的项标识。
    /// </summary>
    public DockItemId Id { get; }

    /// <summary>
    ///     项的行为种类：决定承载组与允许的能力集。
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    ///     显示标题；构造时未提供则回退为 <see cref="Id"/> 的字符串值。
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     可选的与 UI 无关的图标标识；<see langword="null"/> 表示无图标。
    /// </summary>
    public string? IconKey { get; }

    /// <summary>
    ///     内容引用：渲染种类与资源标识，也是内容身份比较与去重判定的依据。
    /// </summary>
    public ContentReference Content { get; }

    /// <summary>
    ///     项的稳定能力集：声明该项支持的用户操作，构造时按种类校验。
    /// </summary>
    public DockItemCapabilities Capabilities { get; }

    /// <summary>
    ///     文档项的固定/预览状态；工具项恒为 <see langword="null"/>。
    /// </summary>
    public DocumentState? DocumentState { get; }

    private static void ValidateCapabilities(
        DockItemKind kind,
        DockItemCapabilities capabilities)
    {
        if ((capabilities & ~KnownCapabilities) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capabilities),
                capabilities,
                "The capability set contains unknown flags.");
        }

        var allowedCapabilities = kind == DockItemKind.Tool
            ? ToolCapabilities
            : DocumentCapabilities;

        if ((capabilities & ~allowedCapabilities) != 0)
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
