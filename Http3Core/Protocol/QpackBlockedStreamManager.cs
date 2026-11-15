namespace Http3Core.Protocol;

public sealed class QpackBlockedStreamManager
{
    public int BlockedStreamCount
    {
        get
        {
            lock (sync)
                return blockedStreams.Count;
        }
    }

    public QpackBlockedStreamManager(int maximumBlockedStreams, QpackInsertCountTracker insertCountTracker)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBlockedStreams);

        this.maximumBlockedStreams = maximumBlockedStreams;
        this.insertCountTracker = insertCountTracker;
    }

    public async Task WaitForInsertCountAsync(long streamId, QpackDynamicTable table, ulong requiredInsertCount, CancellationToken cancellationToken = default)
    {
        // No blocking is necessary.
        if ((ulong)table.InsertCount >= requiredInsertCount)
            return;

        lock (sync)
        {
            // Check again because an insertion might have happened
            // since the first check.
            if ((ulong)table.InsertCount >= requiredInsertCount)
                return;

            if (blockedStreams.TryGetValue(streamId, out int references))
                // This stream is already blocked. Count it only once.
                blockedStreams[streamId] = references + 1;
            else
            {
                if (blockedStreams.Count >= maximumBlockedStreams)
                    throw new InvalidDataException("The peer exceeded SETTINGS_QPACK_BLOCKED_STREAMS.");

                blockedStreams.Add(streamId, 1);
            }
        }

        try
        {
            await insertCountTracker.WaitForInsertCountAsync(table, requiredInsertCount, cancellationToken);
        }
        finally
        {
            lock (sync)
            {
                int references = blockedStreams[streamId];

                if (references == 1)
                    blockedStreams.Remove(streamId);
                else
                    blockedStreams[streamId] = references - 1;
            }
        }
    }

    readonly object sync = new();
    readonly Dictionary<long, int> blockedStreams = [];
    readonly int maximumBlockedStreams;
    readonly QpackInsertCountTracker insertCountTracker;

}