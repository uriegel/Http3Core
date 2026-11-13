namespace Http3Core.Protocol;

public sealed class QpackDynamicTable
{
    public int Count => entries.Count;
    public int InsertCount { get; private set; }
    public int CurrentSize { get; private set; }
    public int MaxSize { get; set; } = 4096;

    public void Insert(string name, string value)
    {
        int size = GetEntrySize(name, value);
        if (size > MaxSize)
            throw new InvalidOperationException("Entry is larger than the maximum dynamic table size.");

        while (CurrentSize + size > MaxSize)
            EvictOldest();

        entries.Insert(0, new QpackHeaderField(name, value));

        CurrentSize += size;
        InsertCount++;
    }

    public QpackHeaderField Get(int absoluteIndex)
    {
        if (absoluteIndex < 0 || absoluteIndex >= entries.Count)
            throw new ArgumentOutOfRangeException(nameof(absoluteIndex));

        return entries[absoluteIndex];
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