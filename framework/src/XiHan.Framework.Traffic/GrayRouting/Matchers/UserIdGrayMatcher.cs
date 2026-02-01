#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UserIdGrayMatcher
// Guid:4f5a6b7c-8d9e-0f1a-2b3c-4d5e6f7a8b9c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
        if (string.IsNullOrEmpty(context.UserId))
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

            return config.UserIds.Contains(context.UserId, StringComparer.OrdinalIgnoreCase);
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
        public List<string> UserIds { get; set; } = [];
    }
}
