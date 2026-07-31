using Microsoft.AspNetCore.Components;

namespace Atlas.Blazor.Content;

internal sealed record AtlasContentRegistration(string Kind, Type ComponentType);

internal sealed class AtlasContentRegistry
{
    private readonly IReadOnlyDictionary<string, Type> componentTypes;

    internal AtlasContentRegistry(IEnumerable<AtlasContentRegistration> registrations)
    {
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            if (!types.TryAdd(registration.Kind, registration.ComponentType))
            {
                throw new InvalidOperationException(
                    $"Atlas content kind '{registration.Kind}' is registered more than once.");
            }
        }

        componentTypes = types;
    }

    internal bool TryResolve(string kind, out Type componentType)
    {
        return componentTypes.TryGetValue(kind, out componentType!);
    }

    internal static AtlasContentRegistration Create<TComponent>(string kind)
        where TComponent : AtlasContentComponent
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("A content kind cannot be empty.", nameof(kind));
        }

        return new AtlasContentRegistration(kind, typeof(TComponent));
    }
}
