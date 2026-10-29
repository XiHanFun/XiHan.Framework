// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件属性来源。
/// </summary>
/// <remarks>
/// 按规范名取值，求值器只取被子句引用到的属性。应用提供主体、资源与环境属性的来源。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IConditionAttributeSource
{
    /// <summary>
    /// 取属性值。
    /// </summary>
    /// <remarks>
    /// 来源不认识这个属性时返回 <see cref="ConditionAttributeValue.Missing"/>。
    /// </remarks>
    /// <param name="canonicalName">规范名（见 <see cref="ConditionAttributeResolution.CanonicalName"/>）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken);
}
