#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotBuilder
// Guid:bc266322-d022-4785-93d6-5e7b50f4be80
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:47:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Providers.DingTalk;
using XiHan.Framework.Bot.Providers.Email;
using XiHan.Framework.Bot.Providers.Lark;
using XiHan.Framework.Bot.Providers.Telegram;
using XiHan.Framework.Bot.Providers.WeCom;
using XiHan.Framework.Bot.Template;

namespace XiHan.Framework.Bot.Extensions;

/// <summary>
/// Bot 构建器
/// </summary>
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

    /// <summary>
    /// 注册钉钉提供者
    /// </summary>
    public BotBuilder UseDingTalk(Action<DingTalkOptions> configure)
    {
        Services.Configure(configure);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, DingTalkBotProvider>());
        return this;
    }

    /// <summary>
    /// 注册飞书提供者
    /// </summary>
    public BotBuilder UseLark(Action<LarkOptions> configure)
    {
        Services.Configure(configure);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, LarkBotProvider>());
        return this;
    }

    /// <summary>
    /// 注册企业微信提供者
    /// </summary>
    public BotBuilder UseWeCom(Action<WeComOptions> configure)
    {
        Services.Configure(configure);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, WeComBotProvider>());
        return this;
    }

    /// <summary>
    /// 注册 Telegram 提供者
    /// </summary>
    public BotBuilder UseTelegram(Action<TelegramOptions> configure)
    {
        Services.Configure(configure);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, TelegramBotProvider>());
        return this;
    }

    /// <summary>
    /// 注册邮件提供者
    /// </summary>
    public BotBuilder UseEmail(Action<EmailOptions> configure)
    {
        Services.Configure(configure);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, EmailBotProvider>());
        return this;
    }
}
