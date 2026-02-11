#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TenantIdGrayMatcher
// Guid:5a6b7c8d-9e0f-1a2b-3c4d-5e6f7a8b9c0d
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
/// 租户ID灰度匹配器
/// </summary>
/// <remarks>
/// 根据指定的租户ID列表判断是否命中灰度
/// </remarks>
public class TenantIdGrayMatcher : IGrayMatcher
{
    /// <summary>
    /// 匹配规则类型
    /// </summary>
    public GrayRuleType RuleType => GrayRuleType.TenantId;

    /// <summary>
    /// 判断是否命中灰度规则
    /// </summary>
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        if (!context.TenantId.HasValue)
        {
            return false;
        }

        if (rule is not GrayRule grayRule || string.IsNullOrEmpty(grayRule.Configuration))
        {
            return false;
        }

        try
        {
            var config = JsonSerializer.Deserialize<TenantIdConfig>(grayRule.Configuration);
            if (config?.TenantIds == null || config.TenantIds.Count == 0)
            {
                return false;
            }

            return config.TenantIds.Contains(context.TenantId.Value);
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
    /// 租户ID配置
    /// </summary>
    private class TenantIdConfig
    {
        /// <summary>
        /// 租户ID列表
        /// </summary>
        public List<long> TenantIds { get; set; } = [];
    }
}
