#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotTelegramModule
// Guid:623b386f-d96b-469d-a7b2-76563bd1bcf8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
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
        context.Services.AddXiHanBotTelegram();
    }
}
