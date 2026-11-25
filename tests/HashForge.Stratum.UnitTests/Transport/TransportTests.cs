using System.Net;
using System.Net.Sockets;
using System.Text;
using HashForge.Stratum.Transport;

namespace HashForge.Stratum.UnitTests.Transport;

public sealed class TransportTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>A stream that hands data out a few bytes at a time, like a slow network.</summary>
    private sealed class TrickleStream(byte[] data, int chunkSize) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(chunkSize, buffer.Length)], cancellationToken);
    }

    private static LineReader Reader(string text, int maxLineLength = 64, int chunkSize = 1024) =>
        new(new TrickleStream(Encoding.UTF8.GetBytes(text), chunkSize), maxLineLength);

    [Fact]
    public async Task GIVEN_ReadLineAsync_WHEN_StreamHasSeveralLines_THEN_ReturnsEachThenNull()
    {
        var reader = Reader("first\nsecond\r\n\nlast\n");

        Assert.Equal("first", await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal("second", await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal("", await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal("last", await reader.ReadLineAsync(CancellationToken.None));
        Assert.Null(await reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_ReadLineAsync_WHEN_DataArrivesInSmallPieces_THEN_LinesAreReassembled()
    {
        var reader = Reader("hello world\nsecond line here\n", maxLineLength: 20, chunkSize: 3);

        Assert.Equal("hello world", await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal("second line here", await reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_ReadLineAsync_WHEN_ACharacterOfSeveralBytesArrivesSplitAcrossReads_THEN_ItIsReadWhole()
    {
        var reader = Reader("Gårdsrigg æøå 矿机\n", maxLineLength: 64, chunkSize: 1);

        Assert.Equal("Gårdsrigg æøå 矿机", await reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_ReadLineAsync_WHEN_LineExceedsLimit_THEN_ThrowsInvalidData()
    {
        var reader = Reader(new string('x', 100) + "\n", maxLineLength: 16);

        await Assert.ThrowsAsync<InvalidDataException>(async () => await reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_ReadLineAsync_WHEN_StreamEndsMidLine_THEN_ReturnsNull()
    {
        Assert.Null(await Reader("no newline").ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_TcpListenerAndConnection_WHEN_ClientConnects_THEN_LinesFlowBothWaysAndCloseIsDetected()
    {
        var factory = new TcpConnectionListenerFactory(StratumTestKit.Options(o => o.MaxLineLength = 256));
        await using var listener = factory.Create(port: 0);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, listener.Port).WaitAsync(Timeout);
        await using var connection = await listener.AcceptAsync(CancellationToken.None).WaitAsync(Timeout);
        using var clientReader = new StreamReader(client.GetStream());

        await client.GetStream().WriteAsync("from miner\n"u8.ToArray());
        var received = await connection.ReadLineAsync(CancellationToken.None).WaitAsync(Timeout);
        await connection.WriteLineAsync("from pool", CancellationToken.None);
        var reply = await clientReader.ReadLineAsync().WaitAsync(Timeout);
        client.Close();
        var afterClose = await connection.ReadLineAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal("from miner", received);
        Assert.Equal("from pool", reply);
        Assert.Null(afterClose);
        Assert.Equal(IPAddress.Loopback, connection.RemoteAddress);
        Assert.NotEqual(0, listener.Port);
    }

    [Fact]
    public async Task GIVEN_TcpListenerFactoryCreate_WHEN_PortIsAlreadyInUse_THEN_ThrowsSocketException()
    {
        var factory = new TcpConnectionListenerFactory(StratumTestKit.Options());
        await using var first = factory.Create(port: 0);

        Assert.Throws<SocketException>(() => factory.Create(first.Port));
    }

    [Fact]
    public async Task GIVEN_AcceptAsync_WHEN_Cancelled_THEN_ThrowsOperationCanceled()
    {
        await using var listener = new TcpConnectionListener(0, 256);
        using var cancellation = new CancellationTokenSource();
        var accepting = listener.AcceptAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => accepting.WaitAsync(Timeout));
    }

    [Fact]
    public async Task GIVEN_ReadLineAsync_WHEN_LineIsLongerThanTheInitialBuffer_THEN_BufferGrowsAndLineIsReadWhole()
    {
        var longLine = new string('x', LineReader.InitialBufferSize * 5);
        var reader = Reader($"short\n{longLine}\nafter\n", maxLineLength: LineReader.InitialBufferSize * 8, chunkSize: 100);

        Assert.Equal("short", await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal(longLine, await reader.ReadLineAsync(CancellationToken.None));
        Assert.Equal("after", await reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_ReadLineAsync_WHEN_LineIsExactlyAtTheLimit_THEN_ItIsStillAccepted()
    {
        const int limit = LineReader.InitialBufferSize * 3;
        var line = new string('x', limit - 1);
        var reader = Reader($"{line}\n", maxLineLength: limit);

        Assert.Equal(line, await reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_ReadLineAsync_WHEN_LineOutgrowsALimitAboveTheInitialBuffer_THEN_ThrowsInvalidData()
    {
        const int limit = LineReader.InitialBufferSize * 3;
        var reader = Reader(new string('x', limit + 1) + "\n", maxLineLength: limit);

        await Assert.ThrowsAsync<InvalidDataException>(async () => await reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_WriteAsync_WHEN_GivenAnEncodedLine_THEN_MinerReceivesExactlyThoseBytes()
    {
        var factory = new TcpConnectionListenerFactory(StratumTestKit.Options());
        await using var listener = factory.Create(port: 0);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, listener.Port).WaitAsync(Timeout);
        await using var connection = await listener.AcceptAsync(CancellationToken.None).WaitAsync(Timeout);
        using var clientReader = new StreamReader(client.GetStream());

        await connection.WriteAsync("already encoded\n"u8.ToArray(), CancellationToken.None);

        Assert.Equal("already encoded", await clientReader.ReadLineAsync().WaitAsync(Timeout));
    }
}
