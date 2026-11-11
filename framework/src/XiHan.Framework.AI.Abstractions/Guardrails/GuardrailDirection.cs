// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// 护栏检查的方向
/// </summary>
/// <remarks>
/// 整数值是线上契约，以后只追加（ADR-0166）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public enum GuardrailDirection
{
    /// <summary>
    /// 发给模型之前的消息
    /// </summary>
    Input = 0,

    /// <summary>
    /// 模型返回的消息
    /// </summary>
    Output = 1,
}
