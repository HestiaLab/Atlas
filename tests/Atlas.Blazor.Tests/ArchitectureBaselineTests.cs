using System.Reflection;
using Microsoft.AspNetCore.Components;
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

    [Fact]
    public void Content_binding_api_leaks_no_dictionaries_string_names_or_component_instances()
    {
        var assembly = Assembly.Load("Atlas.Blazor");
        var contentTypes = assembly.GetTypes()
            .Where(type => type.Namespace == "Atlas.Blazor.Content" && type.IsPublic)
            .ToArray();

        // The immutable Map, builder, and typed binding expose no public constructors.
        var sealedConfigurationTypes = contentTypes
            .Where(type => type.Name is "AtlasContentMap"
                or "AtlasContentMapBuilder"
                or "AtlasComponentBinding`1")
            .ToArray();
        Assert.Equal(3, sealedConfigurationTypes.Length);
        foreach (var type in sealedConfigurationTypes)
        {
            Assert.DoesNotContain(
                type.GetConstructors(),
                constructor => constructor.IsPublic);
        }

        // No public member accepts an arbitrary parameter dictionary or a string
        // parameter name; parameter names are always derived from typed selectors.
        var unsafeParameters = contentTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(method => method.GetParameters().Any(
                parameter => parameter.ParameterType == typeof(Dictionary<string, object?>)
                    || string.Equals(
                        parameter.Name,
                        "parameterName",
                        StringComparison.Ordinal)))
            .ToArray();
        Assert.Empty(unsafeParameters);

        // No public property exposes a mutable parameter bag.
        var dictionaryProperties = contentTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.PropertyType == typeof(Dictionary<string, object?>))
            .ToArray();
        Assert.Empty(dictionaryProperties);

        // No public API returns a naked component instance.
        var instanceAccessorNames = contentTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(method => typeof(ComponentBase).IsAssignableFrom(method.ReturnType))
            .Select(member => $"{member.DeclaringType?.Name}.{member.Name}")
            .Concat(
                contentTypes
                    .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    .Where(property => typeof(ComponentBase).IsAssignableFrom(property.PropertyType))
                    .Select(member => $"{member.DeclaringType?.Name}.{member.Name}"))
            .ToArray();
        Assert.Empty(instanceAccessorNames);

        // The Host exposes no legacy instance lookup or parameter-bag APIs.
        var hostType = assembly.GetType("Atlas.Blazor.Components.AtlasWorkspaceHost");
        Assert.NotNull(hostType);
        var hostMethods = hostType!
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name is "GetContent" or "GetComponent" or "SetContentParameters")
            .ToArray();
        Assert.Empty(hostMethods);
    }
}
