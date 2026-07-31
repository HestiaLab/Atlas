using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Atlas.Blazor.Components;

internal sealed class AtlasContentErrorBoundary : ErrorBoundary
{
    [Parameter]
    public AtlasHostError? SafeError { get; set; }

    [Parameter]
    public EventCallback<AtlasHostError> OnSafeError { get; set; }

    protected override async Task OnErrorAsync(Exception exception)
    {
        await base.OnErrorAsync(exception);
        if (SafeError is not null && OnSafeError.HasDelegate)
        {
            await OnSafeError.InvokeAsync(SafeError);
        }
    }
}
