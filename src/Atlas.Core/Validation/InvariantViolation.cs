namespace Atlas.Core.Validation;

/// <summary>
///     描述一次工作区不变式校验中发现的违规。
/// </summary>
internal sealed class InvariantViolation
{
    internal InvariantViolation(
        InvariantViolationCode code,
        string message,
        LayoutNodeId? nodeId = null,
        DockItemId? itemId = null)
    {
        Code = code;
        Message = message;
        NodeId = nodeId;
        ItemId = itemId;
    }

    /// <summary>
    ///     获取违规的分类代码。
    /// </summary>
    internal InvariantViolationCode Code { get; }

    /// <summary>
    ///     获取人类可读的违规说明。
    /// </summary>
    internal string Message { get; }

    /// <summary>
    ///     获取相关节点标识；无具体节点时为 null。
    /// </summary>
    internal LayoutNodeId? NodeId { get; }

    /// <summary>
    ///     获取相关项标识；无具体项时为 null。
    /// </summary>
    internal DockItemId? ItemId { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var location = string.Empty;
        if (NodeId is { } nodeId)
        {
            var value = nodeId.ToString();
            if (value.Length != 0)
            {
                location += $" node={value}";
            }
        }

        if (ItemId is { } itemId)
        {
            var value = itemId.ToString();
            if (value.Length != 0)
            {
                location += $" item={value}";
            }
        }

        return $"{Code}{location}: {Message}";
    }
}
