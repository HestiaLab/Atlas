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

        if (root.Context is null)
        {
            return;
        }

        if (controller is not null && controllerWorkspaceId != workspaceId)
        {
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
            controller = await module.InvokeAsync<IJSObjectReference>(
                "attachAtlasController",
                root,
                identity,
                receiverReference,
                options);
            controllerWorkspaceId = workspaceId;
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

    private async ValueTask DisposeControllerAsync()
    {
        var current = controller;
        controller = null;
        controllerWorkspaceId = null;
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

    public long Generation { get; set; }

    public long? BaseRevision { get; set; }
}
