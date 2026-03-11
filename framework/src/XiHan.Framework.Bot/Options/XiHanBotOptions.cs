#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotOptions
// Guid:8c924f58-9ca1-407b-8ae5-a611c1902d14
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:43:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Template;

namespace XiHan.Framework.Bot.Options;

/// <summary>
/// Bot 模块配置项
/// </summary>
public class XiHanBotOptions
{
    /// <summary>
    /// 默认策略名称
    /// </summary>
    public string DefaultStrategy { get; set; } = BotStrategyNames.Broadcast;

    /// <summary>
    /// 某个提供者失败后是否继续发送
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// 无可用提供者时是否抛出异常
    /// </summary>
    public bool ThrowWhenNoProvider { get; set; } = false;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 每秒限流条数
    /// </summary>
    public int RateLimitPerSecond { get; set; } = 5;

    /// <summary>
    /// 是否启用日志管道
    /// </summary>
    public bool EnableLoggingPipeline { get; set; } = true;

    /// <summary>
    /// 是否启用重试管道
    /// </summary>
    public bool EnableRetryPipeline { get; set; } = true;

    /// <summary>
    /// 是否启用限流管道
    /// </summary>
    public bool EnableRateLimitPipeline { get; set; } = true;

    /// <summary>
    /// 是否启用环境过滤
    /// </summary>
    public bool EnableEnvironmentFilter { get; set; } = false;

    /// <summary>
    /// 允许发送的环境名称列表
    /// </summary>
    public List<string> AllowedEnvironments { get; set; } = [];

    /// <summary>
    /// 渠道映射
    /// </summary>
    public Dictionary<string, BotChannel> Channels { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 模板映射
    /// </summary>
    public Dictionary<string, BotTemplate> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 添加或替换渠道
    /// </summary>
    public void AddChannel(BotChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (string.IsNullOrWhiteSpace(channel.Name))
        {
            throw new ArgumentException("Channel name is required.", nameof(channel));
        }

        Channels[channel.Name.Trim()] = channel;
    }

    /// <summary>
    /// 添加或替换模板
    /// </summary>
    public void AddTemplate(BotTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new ArgumentException("Template name is required.", nameof(template));
        }

        Templates[template.Name.Trim()] = template;
    }
}
