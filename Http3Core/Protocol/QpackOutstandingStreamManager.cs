namespace Http3Core.Protocol;

public sealed class QpackOutstandingStreamManager
{
    public void Add(long streamId)
    {
        lock (sync)
        {
            streams.TryGetValue(streamId, out int count);
            streams[streamId] = count + 1;
        }
    }

    public void Acknowledge(long streamId)
    {
        lock (sync)
        {
            if (!streams.TryGetValue(streamId, out int count))
                return;

            if (count == 1)
                streams.Remove(streamId);
            else
                streams[streamId] = count - 1;
        }
    }

    public bool Cancel(long streamId)
    {
        lock (sync)
            return streams.Remove(streamId);
    }

    public bool IsOutstanding(long streamId)
    {
        lock (sync)
            return streams.ContainsKey(streamId);
    }

    public int GetOutstandingCount(long streamId)
    {
        lock (sync)
            return streams.GetValueOrDefault(streamId);
    }

    readonly object sync = new();
    readonly Dictionary<long, int> streams = [];
}