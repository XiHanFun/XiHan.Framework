// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.GrayRouting.Abstractions;

/// <summary>
/// 灰度匹配器接口
/// </summary>
/// <remarks>
/// 负责判断请求上下文是否命中某个灰度规则
/// </remarks>
public interface IGrayMatcher
{
    /// <summary>
    /// 匹配规则类型
    /// </summary>
    GrayRuleType RuleType { get; }

    /// <summary>
    /// 判断是否命中灰度规则
    /// </summary>
    /// <param name="context">灰度上下文</param>
    /// <param name="rule">灰度规则</param>
    /// <returns>是否命中</returns>
    bool IsMatch(GrayContext context, IGrayRule rule);

    /// <summary>
    /// 异步判断是否命中灰度规则
    /// </summary>
    /// <param name="context">灰度上下文</param>
    /// <param name="rule">灰度规则</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否命中</returns>
    Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken = default);
}
