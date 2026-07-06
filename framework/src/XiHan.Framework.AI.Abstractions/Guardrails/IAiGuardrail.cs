#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAiGuardrail
// Guid:a1b2c3d4-e5f6-4a21-9c21-0a0b0c0d0e21
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// AI 输入护栏（对入站消息做安全检查，拦截即 fail-closed 拒绝，不下发模型）
/// </summary>
/// <remarks>
/// 检查 seam：默认实现为薄自包含(敏感词/正则黑名单 + 注入启发式)，定位「第一道防线」；
/// 将来可挂 Azure Content Safety 等真检测策略而不动管道。多护栏「全部放行」才通过。
/// </remarks>
public interface IAiGuardrail
{
    /// <summary>
    /// 护栏名（诊断用）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 检查入站消息
    /// </summary>
    ValueTask<GuardrailResult> InspectInputAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);
}
