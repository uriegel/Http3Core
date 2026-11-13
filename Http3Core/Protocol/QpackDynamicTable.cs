namespace Http3Core.Protocol;

public sealed class QpackDynamicTable
{
    public int Count => entries.Count;
    public int InsertCount { get; private set; }
    public int CurrentSize { get; private set; }
    public int MaximumCapacity { get; }
    public int Capacity { get; private set; }

    public QpackDynamicTable(int maximumCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCapacity);
        MaximumCapacity = maximumCapacity;
    }

    public void SetCapacity(int capacity)
    {
        if (capacity < 0 || capacity > MaximumCapacity)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
        while (CurrentSize > Capacity)
            EvictOldest();
    }

    public void Insert(string name, string value)
    {
        int size = GetEntrySize(name, value);

        if (size > Capacity)
            throw new InvalidOperationException("Entry is larger than the dynamic table capacity.");

        while (CurrentSize + size > Capacity)
            EvictOldest();

        entries.Insert(0, new QpackHeaderField(name, value));

        CurrentSize += size;
        InsertCount++;
    }

    public QpackHeaderField Get(int relativeIndex)
    {
        if (relativeIndex < 0 || relativeIndex >= entries.Count)
            throw new ArgumentOutOfRangeException(nameof(relativeIndex));

        return entries[relativeIndex];
    }

    void EvictOldest()
    {
        var entry = entries[^1];
        CurrentSize -= GetEntrySize(entry.Name, entry.Value);
        entries.RemoveAt(entries.Count - 1);
    }

    static int GetEntrySize(string name, string value) =>
        System.Text.Encoding.UTF8.GetByteCount(name) +
        System.Text.Encoding.UTF8.GetByteCount(value) + 32;
    readonly List<QpackHeaderField> entries = [];
}