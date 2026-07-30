// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.Telegram;

/// <summary>
/// 曦寒框架机器人 Telegram 模块
/// </summary>
[DependsOn(
    typeof(XiHanBotModule)
    )]
public class XiHanBotTelegramModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 单发通道（IBotClient 编排用的提供者）
        services.AddXiHanBotTelegram();

        // 多机器人平台（双模传输 / 处理管线 / 发送门面；默认不启用，由配置或应用层 store 开启）
        services.Configure<TelegramBotPlatformOptions>(config.GetSection(TelegramBotPlatformOptions.SectionName));
        services.AddXiHanBotTelegramPlatform();
    }
}
