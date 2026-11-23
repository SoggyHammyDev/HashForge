using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace HashForge.Web.Components;

/// <summary>Base class for pages that show live data: it reloads and re-renders on a timer.</summary>
public abstract class RefreshingComponent : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _disposing = new();
    private ITimer? _timer;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = default!;

    [Inject]
    private IOptions<WebOptions> WebOptions { get; set; } = default!;

    protected DateTimeOffset Now => TimeProvider.GetUtcNow();

    /// <summary>Cancelled when the visitor leaves the page, so abandoned loads stop.</summary>
    protected CancellationToken Cancellation => _disposing.Token;

    public void Dispose()
    {
        _timer?.Dispose();
        _disposing.Cancel();
        _disposing.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Reads whatever the page displays. Called on load and on every refresh.</summary>
    protected virtual Task LoadAsync() => Task.CompletedTask;

    protected override Task OnParametersSetAsync() => LoadAsync();

    protected override void OnAfterRender(bool firstRender)
    {
        // Only interactive renders get here, so a statically pre-rendered page never starts a timer.
        if (firstRender)
        {
            var interval = TimeSpan.FromSeconds(WebOptions.Value.RefreshSeconds);
            _timer = TimeProvider.CreateTimer(_ => _ = InvokeAsync(RefreshAsync), null, interval, interval);
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            await LoadAsync();
            StateHasChanged();
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            // The visitor left while a refresh was in flight.
        }
#pragma warning disable CA1031 // Any failure is handed to Blazor's error handling rather than lost on a timer thread.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            await DispatchExceptionAsync(exception);
        }
    }
}
