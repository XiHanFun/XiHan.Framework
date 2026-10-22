// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件属性来源：按规范名取值，求值器只取被子句引用到的属性
/// </summary>
public interface IConditionAttributeSource
{
    /// <summary>
    /// 取属性值
    /// </summary>
    /// <param name="canonicalName">规范名（见 <see cref="ConditionAttributeResolution.CanonicalName"/>）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>取值；来源不认识这个属性时返回 <see cref="ConditionAttributeValue.Missing"/></returns>
    ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken = default);
}
