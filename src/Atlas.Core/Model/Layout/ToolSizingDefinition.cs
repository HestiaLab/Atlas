namespace Atlas.Core.Layout;

/// <summary>
///     工具项尺寸的受信任元数据：初始比例与最小/最大内容尺寸、折叠尺寸；
///     由宿主声明，工作区据此初始化并约束工具组。
/// </summary>
public readonly struct ToolSizingDefinition : IEquatable<ToolSizingDefinition>
{
    /// <summary>
    ///     构造尺寸定义；初始比例须在 (0, 1] 内、尺寸须有限非负、
    ///     最大不得小于最小、折叠尺寸须有限非负。
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
                || initialRatio.Value > 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialRatio),
                initialRatio,
                "An initial ratio must be finite and greater than zero and at most one.");
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
    ///     工具组初始化与重置时使用的比例；<see langword="null"/> 表示不指定初始比例。
    /// </summary>
    public double? InitialRatio { get; }

    /// <summary>
    ///     工具组的最小内容尺寸；<see langword="null"/> 表示不约束下限。
    /// </summary>
    public double? MinExtent { get; }

    /// <summary>
    ///     工具组的最大内容尺寸；<see langword="null"/> 表示不约束上限。
    /// </summary>
    public double? MaxExtent { get; }

    /// <summary>
    ///     工具组折叠时占据的尺寸；默认 0（完全收起）。
    /// </summary>
    public double CollapsedExtent { get; }

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
    ///     确定两个尺寸定义是否相等。
    /// </summary>
    public static bool operator ==(ToolSizingDefinition left, ToolSizingDefinition right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     确定两个尺寸定义是否不相等。
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
