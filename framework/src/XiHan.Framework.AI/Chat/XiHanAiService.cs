// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
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

    /// <inheritdoc />
    public Task<ChatResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        XiHanChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var client = _resolver.Resolve(options?.Provider);
        return client.GetResponseAsync(messages, options?.ChatOptions, cancellationToken);
    }

    /// <inheritdoc />
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
