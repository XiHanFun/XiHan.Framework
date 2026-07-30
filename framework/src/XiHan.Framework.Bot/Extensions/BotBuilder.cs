// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Template;

namespace XiHan.Framework.Bot.Extensions;

/// <summary>
/// Bot 构建器
/// </summary>
/// <remarks>
/// 主包只提供内核配置（选项/渠道/模板）；各提供者的 Use* 注册方法由对应子包
/// （XiHan.Framework.Bot.Email / .Sms / .Telegram / .DingTalk / .Lark / .WeCom）以扩展方法提供。
/// </remarks>
public sealed class BotBuilder
{
    /// <summary>
    /// 创建构建器
    /// </summary>
    public BotBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// 服务集合
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 配置 Bot 选项
    /// </summary>
    public BotBuilder Configure(Action<XiHanBotOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }

    /// <summary>
    /// 添加渠道映射
    /// </summary>
    public BotBuilder AddChannel(string name, params string[] providers)
    {
        Services.Configure<XiHanBotOptions>(options =>
        {
            options.AddChannel(new BotChannel
            {
                Name = name,
                Providers = providers.Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .ToList()
            });
        });
        return this;
    }

    /// <summary>
    /// 添加模板
    /// </summary>
    public BotBuilder AddTemplate(BotTemplate template)
    {
        Services.Configure<XiHanBotOptions>(options => options.AddTemplate(template));
        return this;
    }
}
