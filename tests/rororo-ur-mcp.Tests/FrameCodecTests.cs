using Labs626.UrMcp.Ipc;

namespace Labs626.UrMcp.Tests;

/// <summary>
/// The codec is a COPY of ur-task's — the wire format is the contract, and each repo pins its own
/// end. These mirror the shapes ur-task's own FrameCodecTests pin: 4-byte big-endian prefix,
/// exact round-trip, 64 KB cap both directions, clean-EOF null, truncation loud.
/// </summary>
public class FrameCodecTests
{
    [Fact]
    public async Task RoundTrips_APayload()
    {
        using var ms = new MemoryStream();
        var payload = "{\"method\":\"ListMacros\"}"u8.ToArray();

        await FrameCodec.WriteFrameAsync(ms, payload, default);
        ms.Position = 0;
        var back = await FrameCodec.ReadFrameAsync(ms, default);

        Assert.Equal(payload, back);
        // Big-endian length prefix, byte-checked so an endianness regression cannot round-trip green.
        ms.Position = 0;
        var prefix = new byte[4];
        Assert.Equal(4, await ms.ReadAsync(prefix));
        Assert.Equal(payload.Length, (prefix[0] << 24) | (prefix[1] << 16) | (prefix[2] << 8) | prefix[3]);
    }

    [Fact]
    public async Task OversizedWrite_Refuses()
    {
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(
            () => FrameCodec.WriteFrameAsync(ms, new byte[FrameCodec.MaxFrameBytes + 1], default));
    }

    [Fact]
    public async Task OversizedLengthPrefix_RefusesOnRead()
    {
        using var ms = new MemoryStream([0x7F, 0xFF, 0xFF, 0xFF]);
        await Assert.ThrowsAsync<InvalidDataException>(() => FrameCodec.ReadFrameAsync(ms, default));
    }

    [Fact]
    public async Task CleanEof_IsNull_TruncationIsLoud()
    {
        using var empty = new MemoryStream();
        Assert.Null(await FrameCodec.ReadFrameAsync(empty, default));

        using var truncated = new MemoryStream([0, 0, 0, 10, 1, 2]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => FrameCodec.ReadFrameAsync(truncated, default));
    }
}
