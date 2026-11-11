// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// 输入护栏
/// </summary>
/// <remarks>
/// <see cref="IAIChat"/> 调用模型前按登记顺序检查全部消息；任一拦截 → <see cref="AIErrors.GuardrailBlocked"/>。以 <c>AddAIGuardrail&lt;T&gt;()</c> 登记
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAIGuardrail
{
    /// <summary>
    /// 名字（进错误的 <c>details.guardrail</c>）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 检查一组消息
    /// </summary>
    /// <remarks>
    /// 输入方向在调用模型之前、输出方向在非流式调用拿到响应之后执行；流式调用只有输入方向，已经推给调用方的内容收不回来。只关心一个方向的护栏对另一个方向直接放行（ADR-0166）
    /// </remarks>
    ValueTask<Result<GuardrailVerdict>> InspectAsync(GuardrailInspection inspection, CancellationToken cancellationToken);
}
