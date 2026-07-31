namespace Atlas.Core.Layout;

/// <summary>
/// Resolves committed split sizing data into deterministic child extents.
/// </summary>
public static class SplitLayoutResolver
{
    /// <summary>
    /// Resolves a two-child split without mutating its committed basis.
    /// </summary>
    /// <param name="availableExtent">The finite, non-negative container extent.</param>
    /// <param name="basis">The committed split basis.</param>
    /// <param name="constraints">The child extent constraints.</param>
    /// <returns>The resolved child extents and clamp state.</returns>
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
