using Atlas.Blazor.Components;
using Atlas.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Atlas.Blazor.Interop;

internal sealed class AtlasRootInteractionController : IAsyncDisposable
{
    private const string ModulePath =
        "./_content/Atlas.Blazor/atlas-v2/atlas.js";
    private readonly IJSRuntime jsRuntime;
    private readonly string hostId;
    private readonly DotNetObjectReference<CallbackReceiver> receiverReference;
    private IJSObjectReference? module;
    private IJSObjectReference? controller;
    private WorkspaceId? controllerWorkspaceId;
    private string? controllerRootId;
    private bool disposed;

    internal AtlasRootInteractionController(
        IJSRuntime jsRuntime,
        string hostId,
        Func<AtlasInteractionMessage, Task> callback)
    {
        this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        if (string.IsNullOrWhiteSpace(hostId))
        {
            throw new ArgumentException("A Host identity is required.", nameof(hostId));
        }

        this.hostId = hostId;
        receiverReference = DotNetObjectReference.Create(
            new CallbackReceiver(
                callback ?? throw new ArgumentNullException(nameof(callback))));
    }

    internal async ValueTask SynchronizeAsync(
        ElementReference root,
        WorkspaceId? workspaceId,
        long generation,
        bool enabled,
        AtlasSplitterOptions splitterOptions,
        AtlasKeyboardCommandMap keyboardCommands)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AtlasRootInteractionController));
        }
        ArgumentNullException.ThrowIfNull(splitterOptions);
        ArgumentNullException.ThrowIfNull(keyboardCommands);
        if (!enabled || workspaceId is null)
        {
            await DisposeControllerAsync();
            return;
        }

        if (controller is not null && controllerWorkspaceId != workspaceId)
        {
            await DisposeControllerAsync();
        }

        if (controller is not null
            && controllerRootId is not null
            && !string.Equals(controllerRootId, root.Id, StringComparison.Ordinal))
        {
            // Blazor replaced the Host root element during a re-render (for
            // example after a workspace revision change). The controller's
            // event listeners are still bound to the detached old element, so
            // dispose and re-attach against the live element.
            await DisposeControllerAsync();
        }

        var identity = new
        {
            hostId,
            workspaceId = workspaceId.Value.Value,
            generation,
            protocolVersion = 1,
        };
        var options = new
        {
            keyboardStepPixels = splitterOptions.KeyboardStepPixels,
            keyboardCommitDelayMilliseconds =
                splitterOptions.KeyboardCommitDelayMilliseconds,
            keyboardBindings = keyboardCommands.Bindings.Select(
                binding => new
                {
                    code = binding.Chord.Code,
                    control = binding.Chord.Control,
                    shift = binding.Chord.Shift,
                    alt = binding.Chord.Alt,
                    meta = binding.Chord.Meta,
                    command = KeyboardCommandName(binding.Command),
                    isGlobal = binding.IsGlobal,
                }).ToArray(),
        };
        if (controller is null)
        {
            module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                ModulePath);

            // Blazor's ElementReference reviver may not have resolved the
            // Host root yet when the render batch's DOM application races
            // OnAfterRenderAsync in WebAssembly (the VS debug proxy makes
            // this window wider). When root.Context is null, pass null so
            // resolveHostElement falls back to the stable data-atlas-host-id
            // attribute; if the element is not in the DOM yet it throws
            // ATLAS-INTERACTION-ATTACH-RETRY and the host retries on the
            // next render instead of silently leaving the controller detached.
            controller = await module.InvokeAsync<IJSObjectReference>(
                "attachAtlasController",
                root.Context is null ? null : root,
                identity,
                receiverReference,
                options);
            controllerWorkspaceId = workspaceId;
            controllerRootId = root.Id;
            return;
        }

        await module!.InvokeVoidAsync(
            "updateAtlasController",
            controller,
            identity,
            options);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await DisposeControllerAsync();
        receiverReference.Dispose();
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSException)
            {
                // The browser circuit has already released the module.
            }

            module = null;
        }
    }

    /// <summary>
    /// Confirms that a split resize commit's outcome is known so the
    /// controller can clear the preview inline styles. For an applied commit
    /// the re-render has already replaced the split's style attribute, so the
    /// clear is a no-op safety net; for a rejected commit it reverts the
    /// layout to the committed ratio.
    /// </summary>
    internal async ValueTask NotifySplitPreviewClearedAsync(string splitId)
    {
        if (disposed || controller is null || module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync(
                "clearAtlasSplitPreview",
                controller,
                splitId);
        }
        catch (JSException)
        {
            // The browser circuit has already released the controller.
        }
    }

    private async ValueTask DisposeControllerAsync()
    {
        var current = controller;
        controller = null;
        controllerWorkspaceId = null;
        controllerRootId = null;
        if (current is null)
        {
            return;
        }

        try
        {
            if (module is not null)
            {
                await module.InvokeVoidAsync(
                    "disposeAtlasController",
                    current);
            }
        }
        catch (JSException)
        {
            // The browser circuit has already released the controller.
        }
        finally
        {
            try
            {
                await current.DisposeAsync();
            }
            catch (JSException)
            {
                // The browser circuit has already released the object reference.
            }
        }
    }

    private static string KeyboardCommandName(AtlasKeyboardCommand command)
    {
        return command switch
        {
            AtlasKeyboardCommand.NextTab => "next-tab",
            AtlasKeyboardCommand.PreviousTab => "previous-tab",
            AtlasKeyboardCommand.CloseActiveDocument => "close-active-document",
            AtlasKeyboardCommand.FocusInlineStart => "focus-inline-start",
            AtlasKeyboardCommand.FocusInlineEnd => "focus-inline-end",
            AtlasKeyboardCommand.FocusBlockStart => "focus-block-start",
            AtlasKeyboardCommand.FocusBlockEnd => "focus-block-end",
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
    }

    private sealed class CallbackReceiver
    {
        private readonly Func<AtlasInteractionMessage, Task> callback;

        internal CallbackReceiver(Func<AtlasInteractionMessage, Task> callback)
        {
            this.callback = callback;
        }

        [JSInvokable]
        public Task OnInteraction(AtlasInteractionMessage message)
        {
            return callback(message);
        }
    }
}

internal sealed class AtlasInteractionMessage
{
    public string? Type { get; set; }

    public string? SplitId { get; set; }

    public double FirstRatio { get; set; }

    public string? Mode { get; set; }

    public string? Command { get; set; }

    public string? SourceGroupId { get; set; }

    public string? TargetGroupId { get; set; }

    public string? TargetItemId { get; set; }

    public int ProtocolVersion { get; set; }

    public string? SessionId { get; set; }

    public string? HostId { get; set; }

    public string? WorkspaceId { get; set; }

    public string? ItemId { get; set; }

    public string? SourceKind { get; set; }

    public string? TargetKind { get; set; }

    public string? LogicalRegion { get; set; }

    public int? InsertionIndex { get; set; }

    public string? NewGroupId { get; set; }

    public string? NewSplitId { get; set; }

    public long Generation { get; set; }

    public long? BaseRevision { get; set; }
}
