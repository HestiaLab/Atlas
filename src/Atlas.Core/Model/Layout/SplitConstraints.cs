namespace Atlas.Core.Layout;

/// <summary>
///     定义分割两个子项的最小与最大尺寸；解析时用于推导可行区间并进行钳制。
/// </summary>
public readonly struct SplitConstraints : IEquatable<SplitConstraints>
{
    /// <summary>
    ///     构造约束集；最小值必须有限且非负，最大值必须有限且不小于对应的最小值。
    /// </summary>
    /// <param name="firstMinimum">第一个子项的有限非负最小尺寸。</param>
    /// <param name="firstMaximum">第一个子项的可选有限最大尺寸。</param>
    /// <param name="secondMinimum">第二个子项的有限非负最小尺寸。</param>
    /// <param name="secondMaximum">第二个子项的可选有限最大尺寸。</param>
    public SplitConstraints(
        double firstMinimum = 0d,
        double? firstMaximum = null,
        double secondMinimum = 0d,
        double? secondMaximum = null)
    {
        ValidateRange(firstMinimum, firstMaximum, nameof(firstMinimum), nameof(firstMaximum));
        ValidateRange(secondMinimum, secondMaximum, nameof(secondMinimum), nameof(secondMaximum));

        FirstMinimum = firstMinimum;
        FirstMaximum = firstMaximum;
        SecondMinimum = secondMinimum;
        SecondMaximum = secondMaximum;
    }

    /// <summary>
    ///     第一个子项的最小尺寸（有限且非负）。
    /// </summary>
    public double FirstMinimum { get; }

    /// <summary>
    ///     第一个子项的最大尺寸；<see langword="null"/> 表示无上限。
    /// </summary>
    public double? FirstMaximum { get; }

    /// <summary>
    ///     第二个子项的最小尺寸（有限且非负）。
    /// </summary>
    public double SecondMinimum { get; }

    /// <summary>
    ///     第二个子项的最大尺寸；<see langword="null"/> 表示无上限。
    /// </summary>
    public double? SecondMaximum { get; }

    /// <summary>
    ///     获取一个值，指示该实例是否为默认约束集（最小值为零且无最大值）。
    ///     默认集合是合法值。
    /// </summary>
    public bool IsDefault =>
        FirstMinimum == 0d
        && FirstMaximum is null
        && SecondMinimum == 0d
        && SecondMaximum is null;

    /// <inheritdoc />
    public bool Equals(SplitConstraints other)
    {
        return FirstMinimum.Equals(other.FirstMinimum)
            && Nullable.Equals(FirstMaximum, other.FirstMaximum)
            && SecondMinimum.Equals(other.SecondMinimum)
            && Nullable.Equals(SecondMaximum, other.SecondMaximum);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SplitConstraints other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            FirstMinimum,
            FirstMaximum,
            SecondMinimum,
            SecondMaximum);
    }

    /// <summary>
    ///     判断两个约束集是否相等。
    /// </summary>
    public static bool operator ==(SplitConstraints left, SplitConstraints right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     判断两个约束集是否不相等。
    /// </summary>
    public static bool operator !=(SplitConstraints left, SplitConstraints right)
    {
        return !left.Equals(right);
    }

    private static void ValidateRange(
        double minimum,
        double? maximum,
        string minimumParameterName,
        string maximumParameterName)
    {
        if (!double.IsFinite(minimum) || minimum < 0d)
        {
            throw new ArgumentOutOfRangeException(
                minimumParameterName,
                minimum,
                "A minimum extent must be finite and non-negative.");
        }

        if (maximum is null)
        {
            return;
        }

        if (!double.IsFinite(maximum.Value) || maximum.Value < minimum)
        {
            throw new ArgumentOutOfRangeException(
                maximumParameterName,
                maximum,
                "A maximum extent must be finite and greater than or equal to its minimum.");
        }
    }
}
