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

    public QpackHeaderField GetAbsolute(int absoluteIndex)
    {
        if (absoluteIndex < 0 || absoluteIndex >= InsertCount)
            throw new ArgumentOutOfRangeException(nameof(absoluteIndex));

        int relativeIndex = InsertCount - 1 - absoluteIndex;
        if (relativeIndex >= entries.Count)
            throw new InvalidOperationException("The requested dynamic table entry has been evicted.");

        return entries[relativeIndex];
    }

    public QpackHeaderField GetRelative(int relativeIndex)
    {
        if (relativeIndex < 0 || relativeIndex >= entries.Count)
            throw new ArgumentOutOfRangeException(nameof(relativeIndex));

        return entries[relativeIndex];
    }

    public QpackHeaderField GetRelative(int baseIndex, int relativeIndex)
    {
        if (baseIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(baseIndex));

        if (relativeIndex < 0 || relativeIndex >= baseIndex)
            throw new ArgumentOutOfRangeException(nameof(relativeIndex));

        int absoluteIndex = baseIndex - relativeIndex - 1;
        return GetAbsolute(absoluteIndex);
    }

    public QpackHeaderField GetPostBase(int baseIndex, int postBaseIndex)
    {
        if (baseIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(baseIndex));

        if (postBaseIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(postBaseIndex));

        int absoluteIndex = checked(baseIndex + postBaseIndex);

        return GetAbsolute(absoluteIndex);
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