using BLZULSA.Models;

namespace BLZULSA.Services;

public sealed class DashboardState
{
    private readonly Lock gate = new();
    private DashboardSnapshot current = DashboardSnapshot.Empty;
    public DashboardSnapshot Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }
    public event Action? Changed;
    internal void Update(Func<DashboardSnapshot, DashboardSnapshot> mutate)
    {
        DashboardSnapshot before;
        DashboardSnapshot after;

        lock (gate)
        {
            before = current;
            after = mutate(current);
            current = after;
        }

        if (before != after)
        {
            NotifyChanged();
        }
    }

    internal void Publish(DashboardSnapshot snapshot) => Update(_ => snapshot);
    private void NotifyChanged()
    {
        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (ObjectDisposedException) {}
        }
    }
}
