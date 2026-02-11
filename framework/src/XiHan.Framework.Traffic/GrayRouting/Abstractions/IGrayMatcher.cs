#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IGrayMatcher
// Guid:2b3c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
