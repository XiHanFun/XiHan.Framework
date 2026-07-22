// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.GrayRouting.Matchers;

/// <summary>
/// 请求头灰度匹配器
/// </summary>
/// <remarks>
/// 根据指定的请求头判断是否命中灰度
/// </remarks>
public class HeaderGrayMatcher : IGrayMatcher
{
    /// <summary>
    /// 匹配规则类型
    /// </summary>
    public GrayRuleType RuleType => GrayRuleType.Header;

    /// <summary>
    /// 判断是否命中灰度规则
    /// </summary>
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        if (context.Headers == null || context.Headers.Count == 0)
        {
            return false;
        }

        if (rule is not GrayRule grayRule || string.IsNullOrEmpty(grayRule.Configuration))
        {
            return false;
        }

        try
        {
            var config = JsonSerializer.Deserialize<HeaderConfig>(grayRule.Configuration);
            if (string.IsNullOrEmpty(config?.HeaderName))
            {
                return false;
            }

            if (!context.Headers.TryGetValue(config.HeaderName, out var headerValue))
            {
                return false;
            }

            // 如果配置了期望值，则进行精确匹配
            if (!string.IsNullOrEmpty(config.HeaderValue))
            {
                return string.Equals(headerValue, config.HeaderValue, StringComparison.OrdinalIgnoreCase);
            }

            // 如果没有配置期望值，只要Header存在就命中
            return true;
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
    /// Header配置
    /// </summary>
    private class HeaderConfig
    {
        /// <summary>
        /// Header名称
        /// </summary>
        public string HeaderName { get; set; } = null!;

        /// <summary>
        /// Header期望值（可选）
        /// </summary>
        public string? HeaderValue { get; set; }
    }
}
