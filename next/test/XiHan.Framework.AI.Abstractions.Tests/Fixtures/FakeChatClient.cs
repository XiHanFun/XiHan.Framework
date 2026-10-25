// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 记录收到的消息与选项的聊天客户端：按 <see cref="Reply"/> 应答，或按 <see cref="Throw"/> 抛异常。
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    /// <summary>
    /// 应答文本。
    /// </summary>
    public string Reply { get; set; } = "pong";

    /// <summary>
    /// 调用时抛出的异常。
    /// </summary>
    public Exception? Throw { get; set; }

    /// <summary>
    /// 最近一次收到的消息。
    /// </summary>
    public List<ChatMessage>? LastMessages { get; private set; }

    /// <summary>
    /// 最近一次收到的选项。
    /// </summary>
    public ChatOptions? LastOptions { get; private set; }

    /// <summary>
    /// 被释放的次数。
    /// </summary>
    public int Disposed { get; private set; }

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Record(messages, options);
        return Throw is null ? Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Reply))) : Task.FromException<ChatResponse>(Throw);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Record(messages, options);
        await Task.Yield();
        foreach (var piece in new[] { Reply[..(Reply.Length / 2)], Reply[(Reply.Length / 2)..] })
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, piece);
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose() => Disposed++;

    /// <summary>
    /// 记录。
    /// </summary>
    private void Record(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        LastMessages = messages.ToList();
        LastOptions = options;
    }
}
