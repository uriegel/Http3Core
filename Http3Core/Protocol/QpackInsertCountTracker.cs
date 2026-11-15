namespace Http3Core.Protocol;

public sealed class QpackInsertCountTracker
{
    public void NotifyInsertCountChanged()
    {
        lock (sync)
        {
            var previous = changed;
            changed = CreateSource();
            previous.TrySetResult(true);
        }
    }

    public async Task WaitForInsertCountAsync(QpackDynamicTable table, ulong requiredInsertCount, CancellationToken cancellationToken = default)
    {
        while ((ulong)table.InsertCount < requiredInsertCount)
        {
            Task waitTask;
            lock (sync)
            {
                if ((ulong)table.InsertCount >= requiredInsertCount)
                    return;
                waitTask = changed.Task;
            }
            await waitTask.WaitAsync(cancellationToken);
        }
    }

    static TaskCompletionSource<bool> CreateSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    readonly object sync = new();
    TaskCompletionSource<bool> changed = CreateSource();
}