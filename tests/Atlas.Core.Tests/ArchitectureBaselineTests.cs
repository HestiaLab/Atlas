using System.Reflection;
using Xunit;

namespace Atlas.Core.Tests;

public sealed class ArchitectureBaselineTests
{
    [Fact]
    public void Core_does_not_reference_UI_frameworks()
    {
        var references = Assembly.Load("Atlas.Core")
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("Microsoft.AspNetCore.Components", references);
        Assert.DoesNotContain("Microsoft.JSInterop", references);
        Assert.DoesNotContain("Atlas.Blazor", references);
    }
}
