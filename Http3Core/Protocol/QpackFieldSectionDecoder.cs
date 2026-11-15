namespace Http3Core.Protocol;

public static class QpackFieldSectionDecoder
{
    public static async ValueTask<List<QpackHeaderField>> DecodeStaticAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var prefix = await QpackFieldSectionPrefixDecoder.ReadAsync(stream, cancellationToken);
        if (prefix.EncodedRequiredInsertCount != 0)
            throw new NotSupportedException("Dynamic table references are not supported yet.");

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