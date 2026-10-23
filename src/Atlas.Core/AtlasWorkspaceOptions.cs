using Atlas.Core.Items;
using Atlas.Core.Operations;

namespace Atlas.Core;

/// <summary>
///     Defines immutable workspace creation-time services and history behavior.
/// </summary>
public sealed class AtlasWorkspaceOptions
{
    /// <summary>
    ///     Creates immutable options; history defaults to the default history
    ///     options, and the content identity policy and close guard resolver are
    ///     optional injections.
    /// </summary>
    public AtlasWorkspaceOptions(
        WorkspaceHistoryOptions? history = null,
        IContentIdentityPolicy? contentIdentityPolicy = null,
        IDockItemCloseGuardResolver? closeGuardResolver = null)
    {
        History = history ?? new WorkspaceHistoryOptions();
        ContentIdentityPolicy = contentIdentityPolicy;
        CloseGuardResolver = closeGuardResolver;
    }

    /// <summary>
    ///     Gets the bounded history behavior.
    /// </summary>
    public WorkspaceHistoryOptions History { get; }

    /// <summary>
    ///     Gets the optional custom content identity policy.
    /// </summary>
    public IContentIdentityPolicy? ContentIdentityPolicy { get; }

    /// <summary>
    ///     Gets the optional item close guard resolver.
    /// </summary>
    public IDockItemCloseGuardResolver? CloseGuardResolver { get; }

    /// <summary>
    ///     Gets or initializes the optional callback invoked when a Changed/domain-event
    ///     subscriber throws; callback failures are isolated from event dispatch.
    /// </summary>
    public Action<Exception>? SubscriberFaultSink { get; init; }
}
