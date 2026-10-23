using System.Text.Json;
using Atlas.Core.Layout;
using Xunit;

namespace Atlas.Core.Tests.Model.Layout;

public sealed class SplitGeometryFixtureTests
{
    [Fact]
    public void Shared_golden_vectors_match_the_core_resolver()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "split-geometry",
            "split-geometry-v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();

        Assert.NotEmpty(cases);
        foreach (var item in cases)
        {
            var name = item.GetProperty("name").GetString();
            var extent = item.GetProperty("availableExtent").GetDouble();
            var basisNode = item.GetProperty("basis");
            var basis = basisNode.GetProperty("kind").GetString() == "fixed-pixels"
                ? SplitBasis.FixedPixels(
                    basisNode.GetProperty("value").GetDouble(),
                    basisNode.GetProperty("anchor").GetString() == "Second"
                        ? SplitAnchor.Second
                        : SplitAnchor.First)
                : SplitBasis.Proportional(basisNode.GetProperty("value").GetDouble());
            var constraintsNode = item.GetProperty("constraints");
            var constraints = new SplitConstraints(
                constraintsNode.GetProperty("firstMinimum").GetDouble(),
                ReadNullableDouble(constraintsNode, "firstMaximum"),
                constraintsNode.GetProperty("secondMinimum").GetDouble(),
                ReadNullableDouble(constraintsNode, "secondMaximum"));

            var resolved = SplitLayoutResolver.Resolve(extent, basis, constraints);
            var expected = item.GetProperty("expected");

            Assert.Equal(
                expected.GetProperty("firstExtent").GetDouble(),
                resolved.FirstExtent,
                precision: 6);
            Assert.Equal(
                expected.GetProperty("secondExtent").GetDouble(),
                resolved.SecondExtent,
                precision: 6);
            Assert.Equal(
                expected.GetProperty("isClamped").GetBoolean(),
                resolved.IsClamped);
            Assert.False(resolved.IsDefault, $"{name}: resolver must produce a value");
        }
    }

    private static double? ReadNullableDouble(JsonElement node, string property)
    {
        return node.GetProperty(property).ValueKind == JsonValueKind.Null
            ? null
            : node.GetProperty(property).GetDouble();
    }
}
