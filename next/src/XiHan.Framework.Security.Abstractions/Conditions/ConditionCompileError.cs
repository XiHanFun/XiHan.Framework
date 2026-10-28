// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 子句的编译错误。<see cref="Error"/> 的错误码是 <see cref="SecurityErrors.ConditionInvalid"/>，消息键说明具体原因，按调用方区域本地化后给编辑界面。
/// </summary>
/// <param name="ClauseKey">子句标识。</param>
/// <param name="AttributeName">属性名（原文）。</param>
/// <param name="Error">错误。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionCompileError(string? ClauseKey, string AttributeName, Error Error);
