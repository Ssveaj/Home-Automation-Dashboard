namespace BLZULSA.Services;

public sealed class RefreshTrigger
{
    private TaskCompletionSource signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Waiter => Volatile.Read(ref signal).Task;

    public void Trigger()
    {
        var previous = Interlocked.Exchange(ref signal, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        previous.TrySetResult();
    }
}
