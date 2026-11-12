// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Protocols.CloudEvents.Outbound;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// Webhook 发布方不读接收方的响应体（ADR-0188）
/// </summary>
/// <remarks>
/// 结果只看状态码：响应体再大也不读进内存，读到响应头即判定
/// </remarks>
public sealed class WebhookResponseTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadGateway, false)]
    public async Task ResponseBody_IsNotRead_WhateverTheStatus(HttpStatusCode status, bool succeeds)
    {
        var body = new CountingStream(64 * 1024 * 1024);
        var services = new ServiceCollection();
        services.AddHttpClient(CloudEventsWebhookPublisher.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new RespondingHandler(status, body));
        await using var provider = services.BuildServiceProvider();
        var options = new CloudEventsProtocolOptions { Webhooks = new Dictionary<string, Uri> { ["orders"] = new("https://hooks.test/orders") } };
        var publisher = new CloudEventsWebhookPublisher(provider.GetRequiredService<IHttpClientFactory>(), options, NullLoggerFactory.Instance);

        var result = await publisher.PublishAsync("orders", new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed" }, CancellationToken.None);

        Assert.Equal(succeeds, result.IsSuccess);
        // 修之前 HttpClient 先把 64 MiB 的响应体整个缓冲下来
        Assert.Equal(0, body.Served);
    }

    /// <summary>
    /// 以给定状态码与正文作答的处理器
    /// </summary>
    private sealed class RespondingHandler(HttpStatusCode status, Stream body) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StreamContent(body) });
    }

    /// <summary>
    /// 给出 <c>length</c> 个零字节、记下已交出多少的只读流
    /// </summary>
    private sealed class CountingStream(long length) : Stream
    {
        /// <summary>
        /// 已交出的字节数
        /// </summary>
        private long _served;

        /// <summary>
        /// 已交出的字节数
        /// </summary>
        public long Served => Interlocked.Read(ref _served);

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            var take = (int)Math.Min(count, length - Interlocked.Read(ref _served));
            buffer.AsSpan(offset, take).Clear();
            Interlocked.Add(ref _served, take);
            return take;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
