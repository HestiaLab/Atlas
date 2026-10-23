namespace Atlas.Core.Layout;

/// <summary>
///     Pure resolver for split layouts: resolves the container extent, the committed basis, and the constraints into a deterministic result without modifying the basis.
/// </summary>
public static class SplitLayoutResolver
{
    /// <summary>
    ///     Resolves a two-child split: first computes the preferred sizes from the basis (proportional = share,
    ///     fixed = anchored value), then clamps the preferred values to the feasible range derived from both
    ///     sides' constraints; when the combined minimums exceed the container or the combined maximums fall
    ///     short of it there is no feasible solution, so the bound values are returned directly and flagged as
    ///     clamped.
    /// </summary>
    /// <param name="availableExtent">A finite, non-negative container extent.</param>
    /// <param name="basis">The committed split basis.</param>
    /// <param name="constraints">The child size constraints; used to derive the feasible range.</param>
    /// <returns>
    ///     The resolved child sizes and clamping state.
    /// </returns>
    public static ResolvedSplitLayout Resolve(
        double availableExtent,
        SplitBasis basis,
        SplitConstraints constraints)
    {
        ValidateInput(availableExtent, basis);

        var preferredFirst = ResolvePreferredFirst(availableExtent, basis);
        var preferredSecond = availableExtent - preferredFirst;

        var firstMaximum = constraints.FirstMaximum ?? double.PositiveInfinity;
        var secondMaximum = constraints.SecondMaximum ?? double.PositiveInfinity;
        var minimumTotal = constraints.FirstMinimum + constraints.SecondMinimum;
        var maximumTotal = firstMaximum + secondMaximum;

        // 先处理无可行解的两个极端：双方最小之和超出容器、双方最大之和小于容器——
        // 只能返回边界值并标记钳制，跳过偏好解析。
        if (minimumTotal > availableExtent)
        {
            return new ResolvedSplitLayout(
                constraints.FirstMinimum,
                constraints.SecondMinimum,
                constraints,
                isClamped: true);
        }

        if (maximumTotal < availableExtent)
        {
            return new ResolvedSplitLayout(
                firstMaximum,
                secondMaximum,
                constraints,
                isClamped: true);
        }

        // 可行区间由双方约束互相推导：下限取"自身最小"与"对方取最大后的剩余"的较大者，
        // 上限取"自身最大"与"对方取最小后的剩余"的较小者——在此区间内钳制偏好值，
        // 保证两个子项同时满足约束。
        var feasibleFirstMinimum = Math.Max(
            constraints.FirstMinimum,
            availableExtent - secondMaximum);
        var feasibleFirstMaximum = Math.Min(
            firstMaximum,
            availableExtent - constraints.SecondMinimum);

        var resolvedFirst = Math.Clamp(
            preferredFirst,
            feasibleFirstMinimum,
            feasibleFirstMaximum);
        var resolvedSecond = availableExtent - resolvedFirst;

        // isClamped 供调用方区分"严格按偏好渲染"与"被约束修正"两种结果（UI 可提示约束生效）。
        return new ResolvedSplitLayout(
            resolvedFirst,
            resolvedSecond,
            constraints,
            isClamped: !resolvedFirst.Equals(preferredFirst)
                || !resolvedSecond.Equals(preferredSecond));
    }

    private static void ValidateInput(
        double availableExtent,
        SplitBasis basis)
    {
        if (!double.IsFinite(availableExtent) || availableExtent < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableExtent),
                availableExtent,
                "The available extent must be finite and non-negative.");
        }

        if (basis.IsDefault)
        {
            throw new ArgumentException(
                "A split basis cannot be the default value.",
                nameof(basis));
        }
    }

    private static double ResolvePreferredFirst(double availableExtent, SplitBasis basis)
    {
        if (basis.Kind == SplitBasisKind.Proportional)
        {
            return availableExtent * basis.Value;
        }

        return basis.Anchor == SplitAnchor.First
            ? basis.Value
            : availableExtent - basis.Value;
    }
}
