namespace Http3Core.Protocol;

public static class QpackFieldSectionDecoder
{
    public static async ValueTask<List<QpackHeaderField>> DecodeAsync(Stream stream, QpackDynamicTable dynamicTable,
        QpackInsertCountTracker insertCountTracker, ulong totalNumberOfInserts, uint maximumTableCapacity, Action? onDynamicTableReference = null, CancellationToken cancellationToken = default)
    {
        var prefix = await QpackFieldSectionPrefixDecoder.ReadDecodedAsync(stream, totalNumberOfInserts, maximumTableCapacity, cancellationToken);        
        await insertCountTracker.WaitForInsertCountAsync(dynamicTable, prefix.RequiredInsertCount, cancellationToken);        

        var fields = new List<QpackHeaderField>();
        while (true)
        {
            int firstByte = stream.ReadByte();
            if (firstByte < 0)
                break;

            if ((firstByte & 0xC0) == 0xC0)
            {
                // Indexed Field Line — Static Table
                var entry = await QpackHeaderFieldDecoder.ReadIndexedStaticAsync(new PrefixByteStream((byte)firstByte, stream), cancellationToken);
                fields.Add(new QpackHeaderField(entry.Name, entry.Value));
            }
            else if ((firstByte & 0xC0) == 0x40 && (firstByte & 0x10) != 0)
            {
                // Literal Field Line With Name Reference — Static Table
                var field = await QpackHeaderFieldDecoder.ReadLiteralWithNameReferenceAsync(new PrefixByteStream((byte)firstByte, stream), cancellationToken);
                fields.Add(field);
            }
            else if ((firstByte & 0xE0) == 0x20)
            {
                // Literal Field Line With Literal Name
                var field = await QpackHeaderFieldDecoder.ReadLiteralWithLiteralNameAsync(new PrefixByteStream((byte)firstByte, stream), cancellationToken);
                fields.Add(field);
            }
            else if ((firstByte & 0xC0) == 0x80)
            {
                // Indexed Field Line — Dynamic Table
                var field = await QpackHeaderFieldDecoder.ReadIndexedDynamicAsync(new PrefixByteStream((byte)firstByte, stream),
                    dynamicTable, checked((int)prefix.Base), cancellationToken);

                fields.Add(field);
                onDynamicTableReference?.Invoke();
            }
            else if ((firstByte & 0xC0) == 0x40 && (firstByte & 0x10) == 0)
            {
                // Literal Field Line With Name Reference — Dynamic Table
                var field = await QpackHeaderFieldDecoder.ReadLiteralWithNameReferenceDynamicAsync(new PrefixByteStream((byte)firstByte, stream),
                    dynamicTable, checked((int)prefix.Base), cancellationToken);
                fields.Add(field);
                onDynamicTableReference?.Invoke();
            }
            else if ((firstByte & 0xF0) == 0x10)
            {
                // Indexed Field Line With Post-Base Index
                var field = await QpackHeaderFieldDecoder.ReadIndexedPostBaseAsync(new PrefixByteStream((byte)firstByte, stream),
                    dynamicTable, checked((int)prefix.Base), cancellationToken);
                fields.Add(field);
                onDynamicTableReference?.Invoke();
            }
            else if ((firstByte & 0xF0) == 0x00)
            {
                // Literal Field Line With Post-Base Name Reference
                var field = await QpackHeaderFieldDecoder.ReadLiteralWithPostBaseNameReferenceAsync(new PrefixByteStream((byte)firstByte, stream),
                    dynamicTable, checked((int)prefix.Base), cancellationToken);
                fields.Add(field);
                onDynamicTableReference?.Invoke();
            }
            else
                throw new NotSupportedException($"Unsupported field-line representation: 0x{firstByte:X2}");
        }

        return fields;
    }

    sealed class PrefixByteStream(byte firstByte, Stream inner) : Stream
    {
        public override int ReadByte()
        {
            if (firstBytePending)
            {
                firstBytePending = false;
                return firstByte;
            }

            return inner.ReadByte();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (firstBytePending && count > 0)
            {
                buffer[offset] = firstByte;
                firstBytePending = false;
                return 1;
            }

            return inner.Read(buffer, offset, count);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        bool firstBytePending = true;
    }
}