using Atlas.Core.Layout;
using Xunit;

namespace Atlas.Core.Tests.Model.Layout;

public sealed class SplitBasisTests
{
    [Theory]
    [InlineData(SplitAnchor.First)]
    [InlineData(SplitAnchor.Second)]
    public void Fixed_basis_preserves_extent_and_anchor(SplitAnchor anchor)
    {
        var basis = SplitBasis.FixedPixels(240d, anchor);

        Assert.Equal(SplitBasisKind.FixedPixels, basis.Kind);
        Assert.Equal(240d, basis.Value);
        Assert.Equal(anchor, basis.Anchor);
    }

    [Fact]
    public void Proportional_basis_preserves_first_child_ratio()
    {
        var basis = SplitBasis.Proportional(0.4d);

        Assert.Equal(SplitBasisKind.Proportional, basis.Kind);
        Assert.Equal(0.4d, basis.Value);
        Assert.Null(basis.Anchor);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Fixed_basis_rejects_invalid_extent(double extent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SplitBasis.FixedPixels(extent, SplitAnchor.First));
    }

    [Fact]
    public void Fixed_basis_rejects_unknown_anchor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SplitBasis.FixedPixels(100d, (SplitAnchor)99));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-0.1d)]
    [InlineData(1.1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Proportional_basis_rejects_invalid_ratio(double ratio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SplitBasis.Proportional(ratio));
    }

    [Fact]
    public void Basis_has_value_semantics()
    {
        var first = SplitBasis.FixedPixels(200d, SplitAnchor.Second);
        var same = SplitBasis.FixedPixels(200d, SplitAnchor.Second);
        var different = SplitBasis.FixedPixels(200d, SplitAnchor.First);

        Assert.True(first == same);
        Assert.False(first != same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, different);
    }

    [Fact]
    public void Default_basis_does_not_expose_sizing_data()
    {
        Assert.Throws<InvalidOperationException>(() => default(SplitBasis).Kind);
        Assert.Throws<InvalidOperationException>(() => default(SplitBasis).Value);
        Assert.Throws<InvalidOperationException>(() => default(SplitBasis).Anchor);
    }
}
