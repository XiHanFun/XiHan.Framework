// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Providers;

namespace XiHan.Framework.AI.Chat;

/// <summary>
/// <see cref="IXiHanAiService"/> 默认实现（薄封装:选 provider → 透传 M.E.AI）
/// </summary>
public sealed class XiHanAiService : IXiHanAiService
{
    private readonly IAiChatClientResolver _resolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanAiService(IAiChatClientResolver resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// 一次对话
    /// </summary>
    /// <param name="messages">对话消息序列</param>
    /// <param name="options">对话选项，含 provider 名与原生对话参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型返回的对话响应</returns>
    public Task<ChatResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        XiHanChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var client = _resolver.Resolve(options?.Provider);
        return client.GetResponseAsync(messages, options?.ChatOptions, cancellationToken);
    }

    /// <summary>
    /// 流式对话
    /// </summary>
    /// <param name="messages">对话消息序列</param>
    /// <param name="options">对话选项，含 provider 名与原生对话参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>逐段产出的对话响应更新序列</returns>
    public async IAsyncEnumerable<ChatResponseUpdate> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        XiHanChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = _resolver.Resolve(options?.Provider);
        await foreach (var update in client.GetStreamingResponseAsync(messages, options?.ChatOptions, cancellationToken))
        {
            yield return update;
        }
    }
}
