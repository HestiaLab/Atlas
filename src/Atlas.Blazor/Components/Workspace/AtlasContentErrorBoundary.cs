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
            await OnSafeError.InvokeAsync(SafeError);
        }
    }
}
