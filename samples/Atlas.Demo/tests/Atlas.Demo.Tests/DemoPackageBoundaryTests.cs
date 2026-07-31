#nullable enable
using System.Xml.Linq;
using Xunit;

namespace Atlas.Demo.Tests;

public sealed class DemoPackageBoundaryTests
{
    [Fact]
    public void Demo_consumes_only_published_atlas_packages()
    {
        var root = FindSampleRoot();
        var project = XDocument.Load(Path.Combine(root, "Atlas.Demo.csproj"));
        var references = project.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains("Atlas.Core", references);
        Assert.Contains("Atlas.Blazor", references);
        var projectReferenceElement = "Project" + "Reference";
        Assert.Empty(project.Descendants(projectReferenceElement));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
                .SelectMany(path => XDocument.Load(path).Descendants(projectReferenceElement)),
            _ => true);
    }

    [Fact]
    public void Portable_source_has_no_repository_coupling()
    {
        var root = FindSampleRoot();
        var forbidden = new[]
        {
            "src" + "/Atlas.Core",
            "src" + "\\Atlas.Core",
            "src" + "/Atlas.Blazor",
            "src" + "\\Atlas.Blazor",
            "C:" + "\\Users\\World\\source\\repos\\Atlas",
            "<Compile" + " Include=\"",
        };

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !HasGeneratedSegment(root, path))
            .Where(path => IsTextFile(path));

        foreach (var path in files)
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain(forbidden, value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string FindSampleRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Atlas.Demo.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Atlas.Demo.sln was not found.");
    }

    private static bool HasGeneratedSegment(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || segment.Equals(".packages", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTextFile(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is
            ".cs" or ".csproj" or ".props" or ".razor" or ".css" or ".html" or ".md" or ".config" or ".sln";
    }
}
