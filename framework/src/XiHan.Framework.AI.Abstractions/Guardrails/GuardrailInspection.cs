// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// 一次护栏检查的输入
/// </summary>
/// <remarks>
/// 成员经初始化器给出，以后加上下文（提供方、提示词名、调用方）只加属性，护栏的签名不变（ADR-0166）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record GuardrailInspection
{
    /// <summary>
    /// 检查的方向
    /// </summary>
    public required GuardrailDirection Direction { get; init; }

    /// <summary>
    /// 要检查的消息
    /// </summary>
    /// <remarks>
    /// 输入方向是渲染后的系统提示加全部请求消息；输出方向是模型这次返回的消息
    /// </remarks>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
}
