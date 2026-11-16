namespace Http3Core.Protocol;

public enum QpackDecoderStreamInstructionType
{
    SectionAcknowledgment,
    StreamCancellation,
    InsertCountIncrement
}

public readonly record struct QpackDecoderStreamInstructionResult(
    QpackDecoderStreamInstructionType Type,
    ulong Value
);

public static class QpackDecoderStreamInstruction
{
    public static byte[] EncodeSectionAcknowledgment(long streamId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(streamId);

        var result = QpackInteger.Encode((ulong)streamId, 7);
        result[0] |= 0x80;

        return result;
    }

    public static byte[] EncodeStreamCancellation(long streamId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(streamId);

        var result = QpackInteger.Encode((ulong)streamId, 6);
        result[0] |= 0x40;

        return result;
    }

    public static byte[] EncodeInsertCountIncrement(ulong increment)
    {
        ArgumentOutOfRangeException.ThrowIfZero(increment);

        return QpackInteger.Encode(increment, 6);
    }

    public static async ValueTask<long> ReadSectionAcknowledgmentAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1];
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
        if (bytesRead == 0)
            throw new EndOfStreamException();
        byte firstByte = buffer[0];
        if ((firstByte & 0x80) == 0)
            throw new InvalidDataException("Not a Section Acknowledgment.");

        ulong streamId = await QpackInteger.ReadAsync(stream, 7, (byte)firstByte, cancellationToken);
        if (streamId > long.MaxValue)
            throw new InvalidDataException("Invalid stream ID.");

        return (long)streamId;
    }

    public static async ValueTask<long> ReadStreamCancellationAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1];
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
        if (bytesRead == 0)
            throw new EndOfStreamException();
        byte firstByte = buffer[0];
        if ((firstByte & 0xC0) != 0x40)
            throw new InvalidDataException("Not a Stream Cancellation.");

        ulong streamId = await QpackInteger.ReadAsync(stream, 6, (byte)firstByte, cancellationToken);
        if (streamId > long.MaxValue)
            throw new InvalidDataException("Invalid stream ID.");

        return (long)streamId;
    }

    public static async ValueTask<ulong> ReadInsertCountIncrementAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1];
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
        if (bytesRead == 0)
            throw new EndOfStreamException();
        byte firstByte = buffer[0];
        if ((firstByte & 0xC0) != 0)
            throw new InvalidDataException("Not an Insert Count Increment.");

        ulong increment = await QpackInteger.ReadAsync(stream, 6, (byte)firstByte, cancellationToken);
        if (increment == 0)
            throw new InvalidDataException("Insert Count Increment must be greater than zero.");

        return increment;
    }

    public static async ValueTask<QpackDecoderStreamInstructionResult> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1];
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
        if (bytesRead == 0)
            throw new EndOfStreamException();
        byte first = buffer[0];
        if ((first & 0x80) != 0)
        {
            ulong streamId = await QpackInteger.ReadAsync(stream, 7, first, cancellationToken);
            return new(QpackDecoderStreamInstructionType.SectionAcknowledgment, streamId);
        }

        if ((first & 0xC0) == 0x40)
        {
            ulong streamId = await QpackInteger.ReadAsync(stream, 6, first, cancellationToken);
            return new(QpackDecoderStreamInstructionType.StreamCancellation, streamId);
        }

        ulong increment = await QpackInteger.ReadAsync(stream, 6, first, cancellationToken);
        if (increment == 0)
            throw new InvalidDataException("Insert Count Increment must be greater than zero.");

        return new(QpackDecoderStreamInstructionType.InsertCountIncrement, increment);
    }
}