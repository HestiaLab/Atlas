using Atlas.Core.Items;

namespace Atlas.Core.Persistence;

/// <summary>
///     配置恢复期间使用的解析限制、内容注册检查、缺失内容策略与保留标识。
/// </summary>
public sealed class LayoutRestoreOptions
{
    /// <summary>
    ///     创建选项；缺失内容策略必须是已定义的枚举值，保留标识必须非默认且
    ///     唯一。
    /// </summary>
    public LayoutRestoreOptions(
        LayoutPersistenceLimits? limits = null,
        Func<ContentReference, bool>? isContentRegistered = null,
        MissingContentPolicy missingContentPolicy = MissingContentPolicy.PreservePlaceholder,
        IEnumerable<DockItemId>? reservedItemIds = null)
    {
        if (!Enum.IsDefined(typeof(MissingContentPolicy), missingContentPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingContentPolicy),
                missingContentPolicy,
                "Unknown missing-content policy.");
        }

        Limits = limits ?? new LayoutPersistenceLimits();
        IsContentRegistered = isContentRegistered;
        MissingContentPolicy = missingContentPolicy;
        var reserved = (reservedItemIds ?? Array.Empty<DockItemId>()).ToArray();
        if (reserved.Any(id => id == default) || reserved.Distinct().Count() != reserved.Length)
        {
            throw new ArgumentException(
                "Reserved identities must be non-default and unique.",
                nameof(reservedItemIds));
        }

        ReservedItemIds = Array.AsReadOnly(reserved);
    }

    /// <summary>
    ///     获取解析器资源限制。
    /// </summary>
    public LayoutPersistenceLimits Limits { get; }

    /// <summary>
    ///     获取可选的、无副作用的内容注册谓词。
    /// </summary>
    public Func<ContentReference, bool>? IsContentRegistered { get; }

    /// <summary>
    ///     获取缺失内容行为。
    /// </summary>
    public MissingContentPolicy MissingContentPolicy { get; }

    /// <summary>
    ///     获取目标作用域中必须保留的标识。
    /// </summary>
    public IReadOnlyList<DockItemId> ReservedItemIds { get; }
}
