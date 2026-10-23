using System.Reflection;
using Atlas.Core.Persistence;
using Xunit;

namespace Atlas.Core.Tests;

public sealed class LayeringTests
{
    [Fact]
    public void Persistence_and_model_namespaces_do_not_reference_operations()
    {
        var violations = typeof(LayoutPersistenceLimits).Assembly
            .GetTypes()
            .Where(type => IsContractNamespace(type.Namespace))
            .Where(type => !type.IsCompilerGenerated())
            .SelectMany(ReferencedTypes)
            .Where(referenced => IsOperationsNamespace(referenced.Namespace))
            .Select(referenced => referenced.FullName)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsContractNamespace(string? ns)
    {
        return ns is not null
            && (ns == "Atlas.Core.Model" || ns.StartsWith("Atlas.Core.Model.", StringComparison.Ordinal)
                || ns == "Atlas.Core.Persistence"
                || ns.StartsWith("Atlas.Core.Persistence.", StringComparison.Ordinal));
    }

    private static bool IsOperationsNamespace(string? ns)
    {
        return ns is not null
            && (ns == "Atlas.Core.Operations"
                || ns.StartsWith("Atlas.Core.Operations.", StringComparison.Ordinal));
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (var referenced in Flatten(type.BaseType))
        {
            yield return referenced;
        }

        foreach (var implemented in type.GetInterfaces())
        {
            yield return implemented;
        }

        foreach (var field in type.GetFields(flags))
        {
            foreach (var referenced in Flatten(field.FieldType))
            {
                yield return referenced;
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            foreach (var referenced in Flatten(property.PropertyType))
            {
                yield return referenced;
            }
        }

        foreach (var method in type.GetMethods(flags))
        {
            foreach (var referenced in Flatten(method.ReturnType))
            {
                yield return referenced;
            }

            foreach (var parameter in method.GetParameters())
            {
                foreach (var referenced in Flatten(parameter.ParameterType))
                {
                    yield return referenced;
                }
            }
        }

        foreach (var constructor in type.GetConstructors(flags))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var referenced in Flatten(parameter.ParameterType))
                {
                    yield return referenced;
                }
            }
        }
    }

    private static IEnumerable<Type> Flatten(Type? type)
    {
        if (type is null
            || type == typeof(void)
            || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            yield break;
        }

        yield return type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var referenced in ReferencedTypes(argument))
                {
                    yield return referenced;
                }
            }
        }

        if (type.IsArray)
        {
            foreach (var referenced in Flatten(type.GetElementType()))
            {
                yield return referenced;
            }
        }
    }
}

internal static class TypeExtensions
{
    internal static bool IsCompilerGenerated(this Type type)
    {
        return type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>()
            is not null;
    }
}
