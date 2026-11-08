using BLZULSA.Models;
using Microsoft.AspNetCore.Components;

namespace BLZULSA.Services;

public abstract class DashboardComponentBase : ComponentBase, IDisposable
{
    private bool subscribed;

    [Inject]
    protected DashboardState State { get; set; } = default!;
    protected DashboardSnapshot Data => State.Current;

    protected override void OnInitialized()
    {
        State.Changed += OnStateChanged;
        subscribed = true;
        base.OnInitialized();
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && subscribed)
        {
            State.Changed -= OnStateChanged;
            subscribed = false;
        }
    }
}
