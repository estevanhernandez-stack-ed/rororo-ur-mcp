// Ipc/FrameCodec.cs — byte-identical framing to ur-task's src/Ipc/FrameCodec.cs (the bridge's
// wire format): a 4-byte big-endian length prefix followed by that many UTF-8 JSON bytes, capped
// at 64 KB. Kept as a copy rather than a shared package on purpose — the wire format is the
// contract, and the two repos each pin their own end of it with tests.
using System.Buffers.Binary;

namespace Labs626.UrMcp.Ipc;

internal static class FrameCodec
{
    public const int MaxFrameBytes = 64 * 1024;

    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (payload.Length > MaxFrameBytes)
            throw new InvalidDataException($"Frame too large: {payload.Length} > {MaxFrameBytes}.");

        var lenBuf = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lenBuf, payload.Length);
        await stream.WriteAsync(lenBuf, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var lenBuf = await ReadExactAsync(stream, 4, ct).ConfigureAwait(false);
        if (lenBuf is null) return null; // clean EOF before any bytes

        int len = BinaryPrimitives.ReadInt32BigEndian(lenBuf);
        if (len < 0 || len > MaxFrameBytes)
            throw new InvalidDataException($"Bad frame length: {len}.");

        var payload = await ReadExactAsync(stream, len, ct).ConfigureAwait(false);
        if (payload is null) throw new EndOfStreamException("Truncated frame: length prefix without body.");
        return payload;
    }

    private static async Task<byte[]?> ReadExactAsync(Stream stream, int count, CancellationToken ct)
    {
        if (count == 0) return Array.Empty<byte>();
        var buf = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = await stream.ReadAsync(buf.AsMemory(read, count - read), ct).ConfigureAwait(false);
            if (n == 0) return read == 0 ? null : throw new EndOfStreamException("Truncated frame.");
            read += n;
        }
        return buf;
    }
}
