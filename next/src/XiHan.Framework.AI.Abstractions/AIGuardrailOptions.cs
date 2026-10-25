// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 内置关键词护栏的选项（<c>XiHan:AI:Guardrails</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AIGuardrailOptions
{
    /// <summary>
    /// 是否登记 <see cref="KeywordBlocklistGuardrail"/>；默认 false。
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 被禁止的关键词（不区分大小写的子串）。
    /// </summary>
    public IReadOnlyList<string> BlockedKeywords { get; init; } = [];

    /// <summary>
    /// 提示注入的正则模式（不区分大小写）。
    /// </summary>
    public IReadOnlyList<string> InjectionPatterns { get; init; } = [];

    /// <summary>
    /// 是否启用内置的中英文提示注入模式；默认 true。
    /// </summary>
    public bool UseBuiltInInjectionHeuristics { get; init; } = true;
}
