using Atlas.Core.Layout;
using Xunit;

namespace Atlas.Core.Tests.Model.Layout;

public sealed class SplitConstraintsTests
{
    [Fact]
    public void Default_constraints_are_unbounded_above()
    {
        var constraints = new SplitConstraints();

        Assert.Equal(0d, constraints.FirstMinimum);
        Assert.Null(constraints.FirstMaximum);
        Assert.Equal(0d, constraints.SecondMinimum);
        Assert.Null(constraints.SecondMaximum);
    }

    [Fact]
    public void Constraints_preserve_both_child_ranges()
    {
        var constraints = new SplitConstraints(
            firstMinimum: 80d,
            firstMaximum: 300d,
            secondMinimum: 120d,
            secondMaximum: 600d);

        Assert.Equal(80d, constraints.FirstMinimum);
        Assert.Equal(300d, constraints.FirstMaximum);
        Assert.Equal(120d, constraints.SecondMinimum);
        Assert.Equal(600d, constraints.SecondMaximum);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constraints_reject_invalid_minimum(double minimum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SplitConstraints(firstMinimum: minimum));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SplitConstraints(secondMinimum: minimum));
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constraints_reject_invalid_maximum(double maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SplitConstraints(firstMaximum: maximum));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SplitConstraints(secondMaximum: maximum));
    }

    [Fact]
    public void Constraints_reject_maximum_below_minimum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SplitConstraints(firstMinimum: 100d, firstMaximum: 99d));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SplitConstraints(secondMinimum: 100d, secondMaximum: 99d));
    }

    [Fact]
    public void Constraints_have_value_semantics()
    {
        var first = new SplitConstraints(10d, 20d, 30d, 40d);
        var same = new SplitConstraints(10d, 20d, 30d, 40d);
        var different = new SplitConstraints(10d, 21d, 30d, 40d);

        Assert.True(first == same);
        Assert.False(first != same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, different);
    }
}
