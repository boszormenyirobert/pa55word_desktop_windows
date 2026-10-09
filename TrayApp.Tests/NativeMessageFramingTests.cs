using System.Text;
using TrayApp.Services;
using Xunit;

namespace TrayApp.Tests;

public sealed class NativeMessageFramingTests
{
    [Fact]
    public async Task Framing_PreservesUtf8ByteLengthAndMultipleMessages()
    {
        using var stream = new MemoryStream();
        const string first = "{\"value\":\"\u00e1\"}";
        const string second = """{"ok":true}""";

        await NativePipe.WriteFramedMessageAsync(stream, first, CancellationToken.None);
        await NativePipe.WriteFramedMessageAsync(stream, second, CancellationToken.None);

        Assert.Equal(Encoding.UTF8.GetByteCount(first), BitConverter.ToInt32(stream.ToArray(), 0));
        stream.Position = 0;
        Assert.Equal(first, await NativePipe.ReadFramedMessageAsync(stream, CancellationToken.None));
        Assert.Equal(second, await NativePipe.ReadFramedMessageAsync(stream, CancellationToken.None));
        Assert.Null(await NativePipe.ReadFramedMessageAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Framing_ReadsPartialHeadersAndPayloads()
    {
        const string json = """{"status":"pending"}""";
        using var buffer = new MemoryStream();
        await NativePipe.WriteFramedMessageAsync(buffer, json, CancellationToken.None);
        using var stream = new PartialReadStream(buffer.ToArray());

        Assert.Equal(json, await NativePipe.ReadFramedMessageAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Framing_RejectsTruncatedHeader()
    {
        using var stream = new MemoryStream(new byte[] { 1, 0 });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            NativePipe.ReadFramedMessageAsync(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Framing_RejectsInvalidPayloadLength(int length)
    {
        using var stream = new MemoryStream(BitConverter.GetBytes(length));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            NativePipe.ReadFramedMessageAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Framing_RejectsTruncatedPayload()
    {
        using var stream = new MemoryStream();
        stream.Write(BitConverter.GetBytes(10));
        stream.WriteByte(1);
        stream.Position = 0;

        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            NativePipe.ReadFramedMessageAsync(stream, CancellationToken.None));
    }

    private sealed class PartialReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
        }
    }
}
