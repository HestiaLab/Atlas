using Atlas.Core.Items;

namespace Atlas.Core.Operations;

/// <summary>
///     描述一次打开请求的条目属性；标识符可省略，省略时由工作区生成全局唯一
///     GUID 标识（REQ-F-028）。
/// </summary>
public sealed class OpenItemRequest
{
    /// <summary>
    ///     初始化打开请求并复用 <see cref="DockItem"/> 的不可变性校验规则。
    /// </summary>
    /// <param name="kind">条目的行为种类。</param>
    /// <param name="content">内容渲染器与资源标识符。</param>
    /// <param name="capabilities">条目的稳定能力集；必须与种类匹配。</param>
    /// <param name="id">可选的条目标识符；为 <see langword="null"/> 或默认值时由工作区生成。</param>
    /// <param name="title">显示标题；为 <see langword="null"/> 时使用解析后的标识符。</param>
    /// <param name="iconKey">可选的、与 UI 无关的图标标识符。</param>
    /// <param name="documentState">仅由文档提交的状态；工具条目传入非 <see langword="null"/> 值时会抛出异常。</param>
    public OpenItemRequest(
        DockItemKind kind,
        ContentReference content,
        DockItemCapabilities capabilities,
        DockItemId? id = null,
        string? title = null,
        string? iconKey = null,
        DocumentState? documentState = null)
    {
        // 用哨兵标识构造一次 DockItem，统一校验内容、种类、能力组合与
        // document state 规则，避免两套校验逻辑漂移。
        _ = new DockItem(
            new DockItemId("atlas-open-request"),
            kind,
            content,
            capabilities,
            title,
            iconKey,
            documentState);

        Id = id;
        Kind = kind;
        Content = content;
        Capabilities = capabilities;
        Title = title;
        IconKey = iconKey;
        DocumentState = documentState;
    }

    /// <summary>
    ///     获取调用方指定的条目标识符；<see langword="null"/> 表示由工作区生成。
    /// </summary>
    public DockItemId? Id { get; }

    /// <summary>
    ///     获取条目的行为种类。
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    ///     获取内容引用。
    /// </summary>
    public ContentReference Content { get; }

    /// <summary>
    ///     获取条目的稳定能力集。
    /// </summary>
    public DockItemCapabilities Capabilities { get; }

    /// <summary>
    ///     获取显示标题；为 <see langword="null"/> 时使用解析后的标识符。
    /// </summary>
    public string? Title { get; }

    /// <summary>
    ///     获取可选的图标标识符。
    /// </summary>
    public string? IconKey { get; }

    /// <summary>
    ///     获取文档条目的固定/预览状态；工具条目为 <see langword="null"/>。
    /// </summary>
    public DocumentState? DocumentState { get; }

    /// <summary>
    ///     使用解析后的标识符创建不可变条目。
    /// </summary>
    /// <param name="id">解析后（调用方指定或工作区生成）的标识符。</param>
    /// <returns>
    ///     可提交给打开规划的不可变条目。
    /// </returns>
    internal DockItem CreateItem(DockItemId id)
    {
        return new DockItem(
            id,
            Kind,
            Content,
            Capabilities,
            Title,
            IconKey,
            DocumentState);
    }
}
