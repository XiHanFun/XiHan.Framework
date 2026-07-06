#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GuardrailChatClient
// Guid:b2c3d4e5-f6a7-4b31-9d31-1a1b1c1d1e31
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Guardrails;

namespace XiHan.Framework.AI.Guardrails;

/// <summary>
/// 护栏中间件（<see cref="DelegatingChatClient"/>；管道最外层，入站消息经全部护栏检查后才下发）
/// </summary>
/// <remarks>
/// 任一护栏拦截 → 直接回拒绝话术，不下发内层（fail-closed）；护栏自身抛异常亦视为拦截。
/// v1 仅做输入侧防护(流式/非流式一致)，输出侧脱敏留后续扩展。
/// </remarks>
public sealed class GuardrailChatClient : DelegatingChatClient
{
    private readonly IReadOnlyList<IAiGuardrail> _guardrails;
    private readonly string _refusalMessage;

    /// <summary>
    /// 构造函数
    /// </summary>
    public GuardrailChatClient(IChatClient innerClient, IEnumerable<IAiGuardrail> guardrails, string refusalMessage)
        : base(innerClient)
    {
        _guardrails = guardrails.ToArray();
        _refusalMessage = refusalMessage;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var blockReason = await InspectAsync(messages, cancellationToken);
        return blockReason is not null
            ? new ChatResponse(new ChatMessage(ChatRole.Assistant, _refusalMessage))
            : await base.GetResponseAsync(messages, options, cancellationToken);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var blockReason = await InspectAsync(messages, cancellationToken);
        if (blockReason is not null)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, _refusalMessage);
            yield break;
        }

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    /// <summary>
    /// 逐护栏检查入站消息，返回拦截原因（放行返回 null）
    /// </summary>
    private async ValueTask<string?> InspectAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        if (_guardrails.Count == 0)
        {
            return null;
        }

        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToArray();
        foreach (var guardrail in _guardrails)
        {
            GuardrailResult result;
            try
            {
                result = await guardrail.InspectInputAsync(materialized, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // 护栏自身异常 → fail-closed 拦截
                return $"护栏 {guardrail.Name} 检查异常";
            }

            if (result.IsBlocked)
            {
                return result.Reason ?? "已被安全策略拦截";
            }
        }

        return null;
    }
}
