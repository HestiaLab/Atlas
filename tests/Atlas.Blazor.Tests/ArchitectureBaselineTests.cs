using System.Reflection;
using Atlas.Core;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ArchitectureBaselineTests
{
    [Fact]
    public void Core_workspace_factory_is_public_and_sealed()
    {
        Assert.True(typeof(AtlasWorkspaceFactory).IsPublic);
        Assert.True(typeof(AtlasWorkspaceFactory).IsSealed);
    }

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

        // 不可变的 Map、构建器与强类型绑定不暴露公共构造函数。
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

        // 没有公共成员接受任意参数字典或字符串参数名；参数名始终来自强类型
        // 选择器。
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

        // 没有公共属性暴露可变参数包。
        var dictionaryProperties = contentTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.PropertyType == typeof(Dictionary<string, object?>))
            .ToArray();
        Assert.Empty(dictionaryProperties);

        // 没有公共 API 返回裸组件实例。
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

        // Host 不暴露遗留的实例查找或参数包 API。
        var hostType = assembly.GetType("Atlas.Blazor.Components.AtlasWorkspaceHost");
        Assert.NotNull(hostType);
        var hostMethods = hostType!
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name is "GetContent" or "GetComponent" or "SetContentParameters")
            .ToArray();
        Assert.Empty(hostMethods);
    }
}
