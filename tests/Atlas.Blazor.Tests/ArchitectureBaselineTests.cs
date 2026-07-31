using System.Reflection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ArchitectureBaselineTests
{
    [Fact]
    public void Blazor_references_Core()
    {
        var references = Assembly.Load("Atlas.Blazor")
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("Atlas.Core", references);
    }
}
