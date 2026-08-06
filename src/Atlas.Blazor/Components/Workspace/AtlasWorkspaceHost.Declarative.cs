using Atlas.Blazor.Content;
using Atlas.Blazor.Interop;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>Declarative members of <see cref="AtlasWorkspaceHost"/> (partial).</summary>
public sealed partial class AtlasWorkspaceHost
{
    private async Task ProcessDeclarationAsync()
    {
        if (!declarativeMode || disposed || WorkspaceId is null)
        {
            return;
        }

        if (!collector.TryBuild(WorkspaceId.Value, out var declaration)
            || declaration is null)
        {
            var fingerprint = "invalid";
            if (!string.Equals(processedFingerprint, fingerprint, StringComparison.Ordinal))
            {
                processedFingerprint = fingerprint;
                await ReportErrorAsync(DefinitionError(WorkspaceId));
                await InvokeAsync(StateHasChanged);
            }

            return;
        }

        var currentPresentationFingerprint = PresentationFingerprint(collector);
        var presentationChanged = !string.Equals(
            presentationFingerprint,
            currentPresentationFingerprint,
            StringComparison.Ordinal);
        presentationFingerprint = currentPresentationFingerprint;
        declarativeSplitterPresentations = collector.GetSplitPresentations();
        var currentFingerprint = Fingerprint(declaration);
        if (string.Equals(processedFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            if (presentationChanged)
            {
                await InvokeAsync(StateHasChanged);
            }

            return;
        }

        processedFingerprint = currentFingerprint;
        if (activeWorkspace is null)
        {
            var result = (Factory ?? new AtlasWorkspaceFactory()).Create(
                declaration,
                WorkspaceOptions);
            if (!result.Succeeded)
            {
                await ReportErrorAsync(DefinitionError(WorkspaceId));
            }
            else
            {
                await BindWorkspaceAsync(
                    result.Value!,
                    declarative: true,
                    ShouldOwnWorkspace(declarative: true));
                RememberDeclaration(declaration);
            }

            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!TryMergeRuntimeItems(declaration, out var merged))
        {
            await ReportErrorAsync(DefinitionError(WorkspaceId));
            await InvokeAsync(StateHasChanged);
            return;
        }

        var reconcile = await activeWorkspace.ExecuteAsync(
            new ReconcileDefinitionOperation(
                merged!,
                activeWorkspace.Current.Revision));
        if (reconcile.Status == WorkspaceResultStatus.Failure)
        {
            await ReportErrorAsync(DefinitionError(WorkspaceId));
        }
        else
        {
            hostError = null;
            RememberDeclaration(declaration);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ProcessContentSourcesAsync()
    {
        if (disposed)
        {
            return;
        }

        // Route/Map source generations are staged only when the parent actually
        // re-rendered this Host (OnParametersSet ran). Internal Host rerenders
        // re-execute the parent's ContentRoutes fragment without new parameter
        // values; processing them again would create a perpetual render loop.
        if (!parentContentGenerationSeen)
        {
            return;
        }

        parentContentGenerationSeen = false;

        if (!routeCollector.TryBuild(out var collectedRoutes))
        {
            // A faulting/incomplete collection keeps the previously committed
            // Route/Map generation active and reports a safe definition error.
            if (!routeFaultReported)
            {
                routeFaultReported = true;
                if (OnError.HasDelegate)
                {
                    await OnError.InvokeAsync(
                        new AtlasHostError(
                            AtlasHostErrorCode.DefinitionInvalid,
                            "ATLAS-CONTENT-ROUTE-INVALID",
                            "Atlas.Content.RouteInvalid",
                            activeWorkspace?.Id));
                }
            }

            return;
        }

        routeFaultReported = false;

        // Each parent render cycle stages one candidate Host-local source
        // generation. The candidate commits atomically after route collection
        // completes: the rebuilt Catalog reference invalidates the group render
        // stamp so outlets re-evaluate value factories and current route
        // delegates. Value-only updates therefore preserve component identity
        // while delivering the complete bound Parameter set on the follow-up
        // projection. Internal Host rerenders never reach this path.
        var previous = committedContentGeneration;
        var stagedMap = ContentMap?.Entries ?? Array.Empty<AtlasContentMapEntry>();
        var generation = BuildContentGeneration(previous, collectedRoutes, stagedMap);
        committedContentGeneration = generation;
        effectiveCatalog = (Registry ?? AtlasContentCatalog.Empty).WithHostLocal(generation);
        await InvokeAsync(StateHasChanged);
    }

    private IReadOnlyList<AtlasContentHostLocalRegistration> BuildContentGeneration(
        IReadOnlyList<AtlasContentHostLocalRegistration> previous,
        IReadOnlyList<AtlasContentRouteDescriptor> collectedRoutes,
        IReadOnlyList<AtlasContentMapEntry> stagedMap)
    {
        var previousByKind = previous
            .GroupBy(registration => registration.Kind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var generation = new List<AtlasContentHostLocalRegistration>(
            collectedRoutes.Count + stagedMap.Count);
        foreach (var route in collectedRoutes)
        {
            // Same Kind and source owner carries the structural revision forward so
            // value-only updates preserve component identity; structural changes
            // (new Kind, removed Kind, or owner switch) remount the outlet.
            var revision = previousByKind.TryGetValue(route.Kind, out var previousRoute)
                && previousRoute.Source == AtlasContentRegistrationSource.Route
                ? previousRoute.Revision
                : nextContentRevision++;
            generation.Add(
                new AtlasContentHostLocalRegistration(
                    route.Kind,
                    AtlasContentRegistrationSource.Route,
                    route.ChildContent!,
                    revision,
                    StructuralSignature: "Route"));
        }

        foreach (var entry in stagedMap)
        {
            // The map structural signature (owner, component type, ordered bound
            // parameter names) drives revision carry-forward; value factories and
            // their results are never structural.
            var revision = previousByKind.TryGetValue(entry.Kind, out var previousEntry)
                && previousEntry.Source == AtlasContentRegistrationSource.Map
                && string.Equals(
                    previousEntry.StructuralSignature,
                    entry.StructuralSignature,
                    StringComparison.Ordinal)
                ? previousEntry.Revision
                : nextContentRevision++;
            generation.Add(
                new AtlasContentHostLocalRegistration(
                    entry.Kind,
                    AtlasContentRegistrationSource.Map,
                    entry.CreateRenderer(),
                    revision,
                    entry.StructuralSignature));
        }

        return generation;
    }

    private bool TryMergeRuntimeItems(
        AtlasWorkspaceDefinition declaration,
        out AtlasWorkspaceDefinition? merged)
    {
        merged = null;
        if (activeWorkspace is null || previousDeclaration is null)
        {
            merged = declaration;
            return true;
        }

        var current = activeWorkspace.Current;
        var runtimeIds = current.Items.Keys
            .Where(itemId => !declaredItemIds.Contains(itemId))
            .ToHashSet();
        var placedRuntimeIds = current.Nodes.Values
            .OfType<GroupNode>()
            .SelectMany(group => group.ItemIds)
            .Concat(
                current.AutoHideStrips.Values.SelectMany(
                    strip => strip.Entries.Select(entry => entry.ItemId)))
            .Where(runtimeIds.Contains)
            .Distinct()
            .ToArray();
        if (placedRuntimeIds.Length != runtimeIds.Count)
        {
            return false;
        }

        var runtimeItems = placedRuntimeIds
            .Select(itemId => current.Items[itemId])
            .ToArray();
        if (runtimeItems.Any(item => declaration.Items.Any(candidate => candidate.Id == item.Id)))
        {
            return false;
        }

        var runtimeByGroup = new Dictionary<LayoutNodeId, List<DockItem>>();
        foreach (var currentGroup in current.Nodes.Values.OfType<GroupNode>())
        {
            var groupRuntimeItems = currentGroup.ItemIds
                .Where(runtimeIds.Contains)
                .Select(itemId => current.Items[itemId])
                .ToList();
            if (groupRuntimeItems.Count == 0)
            {
                continue;
            }

            if (!declaration.Nodes.Any(
                node => node is GroupNode target
                    && target.Id == currentGroup.Id
                    && target.Kind == currentGroup.Kind))
            {
                return false;
            }

            runtimeByGroup.Add(currentGroup.Id, groupRuntimeItems);
        }

        var nodes = declaration.Nodes.Select(
            node =>
            {
                if (node is not GroupNode group
                    || !runtimeByGroup.TryGetValue(group.Id, out var additions))
                {
                    return node;
                }

                var ids = group.ItemIds.Concat(additions.Select(item => item.Id)).ToArray();
                var currentGroup = (GroupNode)current.Nodes[group.Id];
                var selected = currentGroup.SelectedItemId is DockItemId selectedId
                    && additions.Any(item => item.Id == selectedId)
                    ? selectedId
                    : group.SelectedItemId;
                return new GroupNode(
                    group.Id,
                    group.Kind,
                    group.RetentionPolicy,
                    group.Visibility,
                    ids,
                    selected,
                    group.EditorState,
                    group.LogicalRegion);
            }).ToArray();
        var orders = nodes.OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Tool)
            .Select(
                group =>
                {
                    var declarationOrder = group.ItemIds
                        .Where(itemId => !runtimeIds.Contains(itemId));
                    var runtimeOrder = current.ToolBarOrders.TryGetValue(
                        group.LogicalRegion!.Value,
                        out var currentOrder)
                        ? currentOrder.ItemIds.Where(runtimeIds.Contains)
                        : group.ItemIds.Where(runtimeIds.Contains);
                    return new ToolBarOrderState(
                        group.LogicalRegion.Value,
                        declarationOrder.Concat(runtimeOrder));
                });
        var autoHideStrips = declaration.AutoHideStrips.Concat(
            current.AutoHideStrips.Values
                .Select(
                    strip => new AutoHideStripState(
                        strip.Edge,
                        strip.Entries.Where(entry => runtimeIds.Contains(entry.ItemId))))
                .Where(strip => strip.Entries.Count != 0));
        merged = new AtlasWorkspaceDefinition(
            declaration.WorkspaceId,
            declaration.RootNodeId,
            nodes,
            declaration.Items.Concat(runtimeItems),
            declaration.ToolBars,
            orders,
            autoHideStrips);
        return true;
    }

    private void RememberDeclaration(AtlasWorkspaceDefinition declaration)
    {
        previousDeclaration = declaration;
        declaredItemIds = declaration.Items.Select(item => item.Id).ToHashSet();
    }

    private AtlasHostError DefinitionError(WorkspaceId? workspaceId)
    {
        return new AtlasHostError(
            AtlasHostErrorCode.DefinitionInvalid,
            "ATLAS-DEFINITION-INVALID",
            "Atlas.Definition.Invalid",
            workspaceId);
    }

    private bool ShouldOwnWorkspace(bool declarative)
    {
        return WorkspaceOwnership
                == global::Atlas.Blazor.Components.WorkspaceOwnership.Host
            || (WorkspaceOwnership
                    == global::Atlas.Blazor.Components.WorkspaceOwnership.Automatic
                && declarative);
    }

    private void ValidateOwnership()
    {
        if (WorkspaceOwnership
            is < global::Atlas.Blazor.Components.WorkspaceOwnership.Automatic
            or > global::Atlas.Blazor.Components.WorkspaceOwnership.Host)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WorkspaceOwnership),
                WorkspaceOwnership,
                "Unknown workspace ownership.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AtlasWorkspaceHost));
        }
    }

    private static string Fingerprint(AtlasWorkspaceDefinition definition)
    {
        return string.Join(
            "|",
            new[]
            {
                definition.WorkspaceId.Value,
                definition.RootNodeId?.Value ?? string.Empty,
                string.Join(",", definition.Nodes.Select(NodeFingerprint)),
                string.Join(",", definition.Items.Select(ItemFingerprint)),
            });
    }

    private void ResetDeclarationLedger()
    {
        previousDeclaration = null;
        declaredItemIds.Clear();
        processedFingerprint = null;
        presentationFingerprint = null;
        declarativeSplitterPresentations =
            new Dictionary<LayoutNodeId, AtlasSplitterPresentation>();
    }

    private static string PresentationFingerprint(AtlasDefinitionCollector source)
    {
        return string.Join(
            "|",
            source.GetSplitPresentations()
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(
                    pair =>
                        $"{pair.Key}:{pair.Value.IsLocked}:{pair.Value.CssClass}:"
                        + $"{pair.Value.CanCollapseFirst}:{pair.Value.CanCollapseSecond}"));
    }

    private static string NodeFingerprint(LayoutNode node)
    {
        return node switch
        {
            SplitNode split => $"S:{split.Id}:{split.Axis}:{split.Basis.Kind}:{split.Basis.Value}:{split.FirstId}:{split.SecondId}",
            GroupNode group => $"G:{group.Id}:{group.Kind}:{group.RetentionPolicy}:{group.Visibility}:{group.LogicalRegion}:{group.SelectedItemId}:{string.Join("/", group.ItemIds)}",
            _ => node.Id.Value,
        };
    }

    private static string ItemFingerprint(DockItem item)
    {
        return $"I:{item.Id}:{item.Kind}:{item.Content.Kind}:{item.Content.ResourceId}:{item.Title}:{item.IconKey}:{item.Capabilities}:{item.DocumentState?.IsPinned}";
    }

}
