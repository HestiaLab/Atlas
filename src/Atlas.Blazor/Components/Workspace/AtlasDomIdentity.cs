using System.Text;
using Atlas.Core;

namespace Atlas.Blazor.Components;

internal static class AtlasDomIdentity
{
    internal static string ControlId(string hostId, DockItemId itemId, string kind)
    {
        return $"atlas-v2-{kind}-{Token(hostId)}-{Token(itemId.Value)}";
    }

    internal static string PanelId(string hostId, DockItemId itemId)
    {
        return $"atlas-v2-panel-{Token(hostId)}-{Token(itemId.Value)}";
    }

    internal static string HeadingId(string hostId, DockItemId itemId)
    {
        return $"atlas-v2-heading-{Token(hostId)}-{Token(itemId.Value)}";
    }

    private static string Token(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
