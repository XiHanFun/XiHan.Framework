// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// 输入护栏。
/// </summary>
/// <remarks>
/// <see cref="IAIChat"/> 调用模型前按登记顺序检查全部消息；任一拦截 → <see cref="AIErrors.GuardrailBlocked"/>。以 <c>AddAIGuardrail&lt;T&gt;()</c> 登记。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAIGuardrail
{
    /// <summary>
    /// 名字（进错误的 <c>details.guardrail</c>）。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 检查消息。
    /// </summary>
    /// <remarks>
    /// 失败的 <see cref="Result"/> 表示护栏本身出错。
    /// </remarks>
    ValueTask<Result<GuardrailVerdict>> InspectAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken);
}
