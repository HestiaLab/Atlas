using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Atlas.Blazor.Components;

internal sealed class AtlasContentErrorBoundary : ErrorBoundary
{
    private object? previousRecoveryKey;
    private bool recoveryKeyInitialized;

    [Parameter]
    public AtlasHostError? SafeError { get; set; }

    [Parameter]
    public EventCallback<AtlasHostError> OnSafeError { get; set; }

    [Parameter]
    public object? RecoveryKey { get; set; }

    protected override void OnParametersSet()
    {
        if (recoveryKeyInitialized
            && !Equals(previousRecoveryKey, RecoveryKey)
            && CurrentException is not null)
        {
            Recover();
        }

        previousRecoveryKey = RecoveryKey;
        recoveryKeyInitialized = true;
    }

    protected override async Task OnErrorAsync(Exception exception)
    {
        await base.OnErrorAsync(exception);
        if (SafeError is not null && OnSafeError.HasDelegate)
        {
            // F-128 AC2: every failure occurrence receives a new opaque
            // DiagnosticId that does not encode WorkspaceId, ResourceId,
            // DockItemId, or exception type; a later failed Retry gets a new
            // identifier again because this method runs once per occurrence.
            var occurrenceId =
                $"{SafeError.DiagnosticId}-{Guid.NewGuid():N}"[..44];
            var occurrence = new AtlasHostError(
                SafeError.Code,
                occurrenceId,
                SafeError.MessageKey,
                SafeError.WorkspaceId,
                SafeError.ItemId,
                SafeError.NodeId);
            Console.Error.WriteLine(
                $"Atlas content render failure: {occurrenceId}");
            await OnSafeError.InvokeAsync(occurrence);
        }
    }
}
