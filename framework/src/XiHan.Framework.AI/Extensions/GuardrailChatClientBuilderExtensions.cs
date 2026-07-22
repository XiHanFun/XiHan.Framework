// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.AI.Guardrails;

namespace XiHan.Framework.AI.Extensions;

/// <summary>
/// 护栏中间件 <see cref="ChatClientBuilder"/> 扩展
/// </summary>
public static class GuardrailChatClientBuilderExtensions
{
    /// <summary>
    /// 在管道加入内容护栏（应置于最外层，先于遥测/缓存/工具调用）
    /// </summary>
    public static ChatClientBuilder UseContentGuardrail(this ChatClientBuilder builder, IEnumerable<IAiGuardrail> guardrails, string refusalMessage)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Use(innerClient => new GuardrailChatClient(innerClient, guardrails, refusalMessage));
    }
}
