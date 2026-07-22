// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.GrayRouting.Matchers;

/// <summary>
/// 用户ID灰度匹配器
/// </summary>
/// <remarks>
/// 根据指定的用户ID列表判断是否命中灰度
/// </remarks>
public class UserIdGrayMatcher : IGrayMatcher
{
    /// <summary>
    /// 匹配规则类型
    /// </summary>
    public GrayRuleType RuleType => GrayRuleType.UserId;

    /// <summary>
    /// 判断是否命中灰度规则
    /// </summary>
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        if (!context.UserId.HasValue)
        {
            return false;
        }

        if (rule is not GrayRule grayRule || string.IsNullOrEmpty(grayRule.Configuration))
        {
            return false;
        }

        try
        {
            var config = JsonSerializer.Deserialize<UserIdConfig>(grayRule.Configuration);
            if (config?.UserIds == null || config.UserIds.Count == 0)
            {
                return false;
            }

            return config.UserIds.Contains(context.UserId.Value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 异步判断是否命中灰度规则
    /// </summary>
    public Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsMatch(context, rule));
    }

    /// <summary>
    /// 用户ID配置
    /// </summary>
    private class UserIdConfig
    {
        /// <summary>
        /// 用户ID列表
        /// </summary>
        public List<long> UserIds { get; set; } = [];
    }
}
