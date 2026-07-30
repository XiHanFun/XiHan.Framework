// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Sockets;
using XiHan.Framework.AI.Rag;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.AI.Tests;

/// <summary>
/// 嵌入模型异常翻译测试。
/// </summary>
/// <remarks>
/// 嵌入在 RAG 链路上排在向量库之前，其失败最容易被误判成向量库故障，
/// 因此消息必须点明是嵌入环节并带上提供方与模型名。
/// </remarks>
public sealed class EmbeddingOperationTests
{
    /// <summary>
    /// 接口未找到必须翻译成可操作的消息，并指出 OpenAI 兼容端点的常见成因。
    /// </summary>
    /// <remarks>这是配置了错误接口地址或模型名时的真实形态。</remarks>
    [Fact]
    public async Task ExecuteAsync_NotFoundShouldTranslateWithTarget()
    {
        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => EmbeddingOperation.ExecuteAsync<int>(() => throw BuildStatusFailure(404), "openai", "text-embedding-3-small"));

        Assert.Contains("嵌入模型", exception.Message, StringComparison.Ordinal);
        Assert.Contains("openai", exception.Message, StringComparison.Ordinal);
        Assert.Contains("text-embedding-3-small", exception.Message, StringComparison.Ordinal);
        Assert.Contains("/v1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 鉴权、限流与服务端错误都属于提供方侧不可用。
    /// </summary>
    /// <param name="status">HTTP 状态码。</param>
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task ExecuteAsync_ProviderSideFailuresShouldTranslate(int status)
    {
        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => EmbeddingOperation.ExecuteAsync<int>(() => throw BuildStatusFailure(status), "openai", "m"));

        Assert.Contains("嵌入模型", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 请求内容本身的问题必须原样抛出。
    /// </summary>
    /// <remarks>
    /// 单片文本超出模型上下文会得到 400，这是切片策略或输入的缺陷，重试不会好；
    /// 报成依赖不可用会让告警把代码问题误判成基础设施问题。
    /// </remarks>
    /// <param name="status">HTTP 状态码。</param>
    [Theory]
    [InlineData(400)]
    [InlineData(422)]
    public async Task ExecuteAsync_RequestSideFailuresShouldPassThrough(int status)
    {
        var original = BuildStatusFailure(status);

        var thrown = await Assert.ThrowsAsync<ClientResultException>(
            () => EmbeddingOperation.ExecuteAsync<int>(() => throw original, "openai", "m"));

        Assert.Same(original, thrown);
    }

    /// <summary>
    /// 网络层不可达同样翻译，覆盖端点写错主机的情形。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ConnectivityFailureShouldTranslate()
    {
        var inner = new HttpRequestException("connect failed", new SocketException((int)SocketError.ConnectionRefused));

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => EmbeddingOperation.ExecuteAsync<int>(() => throw inner, "local", "bge-m3"));

        Assert.Contains("不可达", exception.Message, StringComparison.Ordinal);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    /// 与业务无关的异常不得被吞成依赖不可用。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_UnrelatedExceptionShouldPassThrough()
    {
        var original = new InvalidOperationException("嵌入模型未配置");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => EmbeddingOperation.ExecuteAsync<int>(() => throw original, "openai", "m"));

        Assert.Same(original, thrown);
    }

    /// <summary>
    /// 构造带 HTTP 状态的 SDK 异常。
    /// </summary>
    private static ClientResultException BuildStatusFailure(int status)
    {
        return new ClientResultException($"Service request failed. Status: {status}", new StubResponse(status));
    }

    /// <summary>
    /// 仅承载状态码的响应替身。
    /// </summary>
    private sealed class StubResponse(int status) : PipelineResponse
    {
        public override int Status { get; } = status;

        public override string ReasonPhrase => string.Empty;

        public override Stream? ContentStream { get; set; }

        public override BinaryData Content => BinaryData.FromString(string.Empty);

        protected override PipelineResponseHeaders HeadersCore { get; } = new StubHeaders();

        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => Content;

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Content);

        public override void Dispose()
        {
        }

        private sealed class StubHeaders : PipelineResponseHeaders
        {
            public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
                => Enumerable.Empty<KeyValuePair<string, string>>().GetEnumerator();

            public override bool TryGetValue(string name, out string? value)
            {
                value = null;
                return false;
            }

            public override bool TryGetValues(string name, out IEnumerable<string>? values)
            {
                values = null;
                return false;
            }
        }
    }
}
