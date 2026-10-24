namespace Atlas.Core.Layout;

/// <summary>
///     工具条目尺寸的可信元数据：初始比例、最小/最大内容尺寸与折叠尺寸；由宿主
///     声明，工作区用它初始化并约束工具 Group。
/// </summary>
public readonly struct ToolSizingDefinition : IEquatable<ToolSizingDefinition>
{
    /// <summary>
    ///     构造尺寸定义；初始比例必须位于开区间 (0, 1) 内，以便作为比例分割的
    ///     初始值；尺寸必须有限且非负，最大值不得小于最小值，折叠尺寸必须有限
    ///     且非负。
    /// </summary>
    public ToolSizingDefinition(
        double? initialRatio = null,
        double? minExtent = null,
        double? maxExtent = null,
        double collapsedExtent = 0d)
    {
        if (initialRatio is not null
            && (!double.IsFinite(initialRatio.Value)
                || initialRatio.Value <= 0d
                || initialRatio.Value >= 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialRatio),
                initialRatio,
                "An initial ratio must be finite and greater than zero and less than one.");
        }

        ValidateOptionalExtent(minExtent, nameof(minExtent));
        ValidateOptionalExtent(maxExtent, nameof(maxExtent));

        if (minExtent is not null
            && maxExtent is not null
            && maxExtent.Value < minExtent.Value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExtent),
                maxExtent,
                "A maximum extent cannot be less than the minimum extent.");
        }

        if (!double.IsFinite(collapsedExtent) || collapsedExtent < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(collapsedExtent),
                collapsedExtent,
                "A collapsed extent must be finite and non-negative.");
        }

        InitialRatio = initialRatio;
        MinExtent = minExtent;
        MaxExtent = maxExtent;
        CollapsedExtent = collapsedExtent;
    }

    /// <summary>
    ///     初始化和重置工具 Group 时使用的比例；<see langword="null"/> 表示未
    ///     指定初始比例。
    /// </summary>
    public double? InitialRatio { get; }

    /// <summary>
    ///     工具 Group 的最小内容尺寸；<see langword="null"/> 表示无下限。
    /// </summary>
    public double? MinExtent { get; }

    /// <summary>
    ///     工具 Group 的最大内容尺寸；<see langword="null"/> 表示无上限。
    /// </summary>
    public double? MaxExtent { get; }

    /// <summary>
    ///     工具 Group 折叠时占用的尺寸；默认为 0（完全折叠）。
    /// </summary>
    public double CollapsedExtent { get; }

    /// <summary>
    ///     获取一个值，指示该实例是否为默认定义（无比例或尺寸）。默认定义是
    ///     合法值。
    /// </summary>
    public bool IsDefault =>
        InitialRatio is null
        && MinExtent is null
        && MaxExtent is null
        && CollapsedExtent == 0d;

    /// <inheritdoc />
    public bool Equals(ToolSizingDefinition other)
    {
        return Nullable.Equals(InitialRatio, other.InitialRatio)
            && Nullable.Equals(MinExtent, other.MinExtent)
            && Nullable.Equals(MaxExtent, other.MaxExtent)
            && CollapsedExtent.Equals(other.CollapsedExtent);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ToolSizingDefinition other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(InitialRatio, MinExtent, MaxExtent, CollapsedExtent);
    }

    /// <summary>
    ///     判断两个尺寸定义是否相等。
    /// </summary>
    public static bool operator ==(ToolSizingDefinition left, ToolSizingDefinition right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     判断两个尺寸定义是否不相等。
    /// </summary>
    public static bool operator !=(ToolSizingDefinition left, ToolSizingDefinition right)
    {
        return !left.Equals(right);
    }

    private static void ValidateOptionalExtent(double? value, string parameterName)
    {
        if (value is not null && (!double.IsFinite(value.Value) || value.Value < 0d))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "An extent must be finite and non-negative.");
        }
    }
}
