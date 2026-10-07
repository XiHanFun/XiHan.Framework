// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using XiHan.Framework.Utils.Net.Sse;

namespace XiHan.Framework.Utils.Tests.Net;

public sealed class SseClientTests
{
    [Fact]
    public async Task ConnectAsync_OversizedLine_FailsAndReportsClose()
    {
        var closed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCount = 0;
        using var client = CreateClient($"data:{new string('x', 100)}\n\n", maxLineBytes: 16, maxEventBytes: 256);
        client.OnClosed += exception =>
        {
            Interlocked.Increment(ref closeCount);
            closed.TrySetResult(exception);
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => client.ConnectAsync("https://example.test/events"));

        Assert.IsType<InvalidDataException>(await closed.Task);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task ConnectAsync_MultipleDataLines_EnforcesEventLimit()
    {
        using var client = CreateClient("data:12345678\ndata:abcdefgh\n\n", maxLineBytes: 32, maxEventBytes: 20);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.ConnectAsync("https://example.test/events"));
    }

    [Fact]
    public async Task ConnectAsync_EventAtExactLimit_DeliversMessage()
    {
        SseMessage? received = null;
        using var client = CreateClient("data:ok\n\n", maxLineBytes: 7, maxEventBytes: 8);
        client.OnMessage += message => received = message;

        await client.ConnectAsync("https://example.test/events");

        Assert.NotNull(received);
        Assert.Equal("ok", received.Data);
    }

    [Fact]
    public async Task ConnectAsync_Utf8SplitAcrossReads_DecodesWithinByteLimit()
    {
        SseMessage? received = null;
        var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes("data:你好\n\n"), chunkSize: 2);
        using var client = CreateClient(stream, maxLineBytes: 11, maxEventBytes: 12);
        client.OnMessage += message => received = message;

        await client.ConnectAsync("https://example.test/events");

        Assert.NotNull(received);
        Assert.Equal("你好", received.Data);
    }

    [Fact]
    public async Task ConnectAsync_Cancellation_DisposesResponseStream()
    {
        var stream = new BlockingReadStream();
        using var client = CreateClient(stream, maxLineBytes: 32, maxEventBytes: 64);
        using var cancellation = new CancellationTokenSource();

        var connect = client.ConnectAsync("https://example.test/events", cancellationToken: cancellation.Token);
        await stream.ReadStarted.Task;
        cancellation.Cancel();
        await connect;

        Assert.True(stream.IsDisposed);
    }

    private static SseClient CreateClient(string payload, int maxLineBytes, int maxEventBytes)
        => CreateClient(new MemoryStream(Encoding.UTF8.GetBytes(payload)), maxLineBytes, maxEventBytes);

    private static SseClient CreateClient(Stream responseStream, int maxLineBytes, int maxEventBytes)
    {
        var handler = new StreamResponseHandler(responseStream);
        return new SseClient(new SseClientOptions
        {
            HttpClient = new HttpClient(handler),
            Timeout = Timeout.InfiniteTimeSpan,
            MaxLineBytes = maxLineBytes,
            MaxEventBytes = maxEventBytes
        });
    }

    private sealed class StaticResponseHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            });
    }

    private sealed class StreamResponseHandler(Stream stream) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            });
    }

    private sealed class ChunkedReadStream(byte[] payload, int chunkSize) : MemoryStream(payload)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
    }

    private sealed class BlockingReadStream : Stream
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            return WaitForCancellationAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private static async ValueTask<int> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
