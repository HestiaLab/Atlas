using System.Text;
using Atlas.Core;
using Atlas.Core.Layout;

namespace Atlas.Blazor.Components;

/// <summary>
///     为组内标签页、面板与标题派生稳定且安全的 DOM id。
/// </summary>
internal static class AtlasDomIdentity
{
    /// <summary>
    ///     为指定种类控件（如文档标签页）派生 DOM id。
    /// </summary>
    /// <param name="hostId">宿主标识，参与派生以保证宿主间唯一。</param>
    /// <param name="itemId">项标识，参与派生以保证组内唯一。</param>
    /// <param name="kind">控件种类令牌，如 document-tab。</param>
    internal static string ControlId(string hostId, DockItemId itemId, string kind)
    {
        return $"atlas-v2-{kind}-{Token(hostId)}-{Token(itemId.Value)}";
    }

    /// <summary>
    ///     为组级控件（如标签页溢出菜单）派生 DOM id。
    /// </summary>
    /// <param name="hostId">宿主标识，参与派生以保证宿主间唯一。</param>
    /// <param name="groupId">组标识，参与派生以保证组间唯一。</param>
    /// <param name="kind">控件种类令牌，如 tab-overflow-menu。</param>
    internal static string GroupControlId(string hostId, LayoutNodeId groupId, string kind)
    {
        return $"atlas-v2-{kind}-{Token(hostId)}-{Token(groupId.Value)}";
    }

    /// <summary>
    ///     为项的内容面板派生 DOM id，供标签页的 aria-controls 引用。
    /// </summary>
    /// <param name="hostId">宿主标识，参与派生以保证宿主间唯一。</param>
    /// <param name="itemId">项标识，参与派生以保证组内唯一。</param>
    internal static string PanelId(string hostId, DockItemId itemId)
    {
        return $"atlas-v2-panel-{Token(hostId)}-{Token(itemId.Value)}";
    }

    /// <summary>
    ///     为工具组头部标题派生 DOM id，供组区域的无障碍标签引用。
    /// </summary>
    /// <param name="hostId">宿主标识，参与派生以保证宿主间唯一。</param>
    /// <param name="itemId">项标识，参与派生以保证组内唯一。</param>
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
