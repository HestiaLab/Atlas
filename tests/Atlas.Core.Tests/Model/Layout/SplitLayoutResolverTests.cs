using System.Reflection;
using Atlas.Core.Layout;
using Xunit;

namespace Atlas.Core.Tests.Model.Layout;

public sealed class SplitLayoutResolverTests
{
    [Fact]
    public void Proportional_basis_resolves_both_children()
    {
        var constraints = new SplitConstraints();

        var result = SplitLayoutResolver.Resolve(
            1000d,
            SplitBasis.Proportional(0.4d),
            constraints);

        Assert.Equal(400d, result.FirstExtent);
        Assert.Equal(600d, result.SecondExtent);
        Assert.Equal(constraints, result.EffectiveConstraints);
        Assert.False(result.IsClamped);
    }

    [Fact]
    public void Fixed_first_basis_resolves_second_child_from_remainder()
    {
        var result = SplitLayoutResolver.Resolve(
            1000d,
            SplitBasis.FixedPixels(240d, SplitAnchor.First),
            new SplitConstraints());

        Assert.Equal(240d, result.FirstExtent);
        Assert.Equal(760d, result.SecondExtent);
        Assert.False(result.IsClamped);
    }

    [Fact]
    public void Fixed_second_basis_resolves_first_child_from_remainder()
    {
        var result = SplitLayoutResolver.Resolve(
            1000d,
            SplitBasis.FixedPixels(240d, SplitAnchor.Second),
            new SplitConstraints());

        Assert.Equal(760d, result.FirstExtent);
        Assert.Equal(240d, result.SecondExtent);
        Assert.False(result.IsClamped);
    }

    [Fact]
    public void Minimum_constraint_clamps_preferred_extent_and_reallocates_space()
    {
        var result = SplitLayoutResolver.Resolve(
            1000d,
            SplitBasis.Proportional(0.1d),
            new SplitConstraints(firstMinimum: 250d));

        Assert.Equal(250d, result.FirstExtent);
        Assert.Equal(750d, result.SecondExtent);
        Assert.True(result.IsClamped);
    }

    [Fact]
    public void Maximum_constraint_clamps_preferred_extent_and_reallocates_space()
    {
        var result = SplitLayoutResolver.Resolve(
            1000d,
            SplitBasis.Proportional(0.8d),
            new SplitConstraints(firstMaximum: 600d));

        Assert.Equal(600d, result.FirstExtent);
        Assert.Equal(400d, result.SecondExtent);
        Assert.True(result.IsClamped);
    }

    [Fact]
    public void Second_child_constraints_are_applied_symmetrically()
    {
        var minimumResult = SplitLayoutResolver.Resolve(
            1000d,
            SplitBasis.Proportional(0.9d),
            new SplitConstraints(secondMinimum: 300d));
        var maximumResult = SplitLayoutResolver.Resolve(
            1000d,
            SplitBasis.Proportional(0.2d),
            new SplitConstraints(secondMaximum: 600d));

        Assert.Equal(700d, minimumResult.FirstExtent);
        Assert.Equal(300d, minimumResult.SecondExtent);
        Assert.True(minimumResult.IsClamped);
        Assert.Equal(400d, maximumResult.FirstExtent);
        Assert.Equal(600d, maximumResult.SecondExtent);
        Assert.True(maximumResult.IsClamped);
    }

    [Fact]
    public void Conflicting_minimums_preserve_constraints_and_allow_overflow()
    {
        var result = SplitLayoutResolver.Resolve(
            500d,
            SplitBasis.Proportional(0.5d),
            new SplitConstraints(firstMinimum: 300d, secondMinimum: 250d));

        Assert.Equal(300d, result.FirstExtent);
        Assert.Equal(250d, result.SecondExtent);
        Assert.True(result.FirstExtent + result.SecondExtent > 500d);
        Assert.True(result.IsClamped);
    }

    [Fact]
    public void Conflicting_maximums_leave_unallocated_extent()
    {
        var result = SplitLayoutResolver.Resolve(
            1000d,
            SplitBasis.Proportional(0.5d),
            new SplitConstraints(firstMaximum: 300d, secondMaximum: 400d));

        Assert.Equal(300d, result.FirstExtent);
        Assert.Equal(400d, result.SecondExtent);
        Assert.True(result.FirstExtent + result.SecondExtent < 1000d);
        Assert.True(result.IsClamped);
    }

    [Fact]
    public void Oversized_fixed_basis_is_clamped_to_container()
    {
        var result = SplitLayoutResolver.Resolve(
            500d,
            SplitBasis.FixedPixels(800d, SplitAnchor.First),
            new SplitConstraints());

        Assert.Equal(500d, result.FirstExtent);
        Assert.Equal(0d, result.SecondExtent);
        Assert.True(result.IsClamped);
    }

    [Fact]
    public void Proportional_basis_is_not_rewritten_by_container_clamping()
    {
        var basis = SplitBasis.Proportional(0.25d);
        var constraints = new SplitConstraints(firstMinimum: 300d);

        var constrained = SplitLayoutResolver.Resolve(
            800d,
            basis,
            constraints);
        var restored = SplitLayoutResolver.Resolve(
            1600d,
            basis,
            constraints);

        Assert.Equal(300d, constrained.FirstExtent);
        Assert.True(constrained.IsClamped);
        Assert.Equal(400d, restored.FirstExtent);
        Assert.Equal(1200d, restored.SecondExtent);
        Assert.False(restored.IsClamped);
        Assert.Equal(0.25d, basis.Value);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Resolver_rejects_invalid_available_extent(double availableExtent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SplitLayoutResolver.Resolve(
                availableExtent,
                SplitBasis.Proportional(0.5d),
                new SplitConstraints()));
    }

    [Fact]
    public void Resolver_rejects_default_basis()
    {
        Assert.Throws<ArgumentException>(
            () => SplitLayoutResolver.Resolve(
                1000d,
                default,
                new SplitConstraints()));
    }

    [Fact]
    public void Result_public_properties_are_read_only()
    {
        var writableProperties = typeof(ResolvedSplitLayout)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanWrite);

        Assert.Empty(writableProperties);
    }
}
