// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 带自己传输超时、在闸门处停住再观察令牌的聊天客户端。
/// </summary>
/// <remarks>
/// 自己的超时与 OpenAI SDK 的 <c>NetworkTimeout</c> 同形：同一时钟、同一时限，完整应答一次、流式每等一个更新一次；
/// 抛出时与 SDK 一样先看交给它的令牌，再看自己的超时。不抛出时接住取消，交回截断的应答或提前结束流。
/// </remarks>
internal sealed class GatedChatClient : IChatClient
{
    /// <summary>
    /// 截断应答的文本。
    /// </summary>
    public const string Truncated = "trunc";

    /// <summary>
    /// 时间源。
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 自己的传输超时。
    /// </summary>
    private readonly TimeSpan _timeout;

    /// <summary>
    /// 闸门。
    /// </summary>
    private readonly ProviderGate _gate;

    /// <summary>
    /// 创建。
    /// </summary>
    public GatedChatClient(TimeProvider timeProvider, TimeSpan timeout, ProviderGate gate)
    {
        _timeProvider = timeProvider;
        _timeout = timeout;
        _gate = gate;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var own = new CancellationTokenSource(_timeout, _timeProvider);
        await _gate.PassAsync();
        return Observe(own, cancellationToken) ? new ChatResponse(new ChatMessage(ChatRole.Assistant, Truncated)) : new ChatResponse(new ChatMessage(ChatRole.Assistant, "pong"));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, "first");

        // 第二个更新在闸门之后：自己的超时按每次读取计时，与 SDK 的流式读取一样
        using var own = new CancellationTokenSource(_timeout, _timeProvider);
        await _gate.PassAsync();
        if (Observe(own, cancellationToken))
        {
            yield break;
        }

        yield return new ChatResponseUpdate(ChatRole.Assistant, "second");
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    /// 观察令牌：抛出模式下按 SDK 的顺序抛出，否则返回是否已被取消。
    /// </summary>
    private bool Observe(CancellationTokenSource own, CancellationToken cancellationToken)
    {
        if (_gate.Throws)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (own.IsCancellationRequested)
            {
                throw new TaskCanceledException("The operation was cancelled because it exceeded the configured timeout.", new TimeoutException(), own.Token);
            }
        }

        return cancellationToken.IsCancellationRequested || own.IsCancellationRequested;
    }
}
