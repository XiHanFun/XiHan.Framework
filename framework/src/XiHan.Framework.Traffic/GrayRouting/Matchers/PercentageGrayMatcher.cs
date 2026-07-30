// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.GrayRouting.Matchers;

/// <summary>
/// 百分比灰度匹配器
/// </summary>
/// <remarks>
/// 根据配置的百分比随机决定是否命中灰度
/// </remarks>
public class PercentageGrayMatcher : IGrayMatcher
{
    /// <summary>
    /// 匹配规则类型
    /// </summary>
    public GrayRuleType RuleType => GrayRuleType.Percentage;

    /// <summary>
    /// 判断是否命中灰度规则
    /// </summary>
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        if (rule is not GrayRule grayRule || string.IsNullOrEmpty(grayRule.Configuration))
        {
            return false;
        }

        try
        {
            var config = JsonSerializer.Deserialize<PercentageConfig>(grayRule.Configuration);
            if (config?.Percentage == null || config.Percentage <= 0)
            {
                return false;
            }

            // 基于随机数判断
            var random = Random.Shared.Next(1, 101);
            return random <= config.Percentage;
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
    /// 百分比配置
    /// </summary>
    private class PercentageConfig
    {
        /// <summary>
        /// 灰度百分比 (1-100)
        /// </summary>
        public int? Percentage { get; set; }
    }
}
