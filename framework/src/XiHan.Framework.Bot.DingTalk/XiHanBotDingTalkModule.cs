// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.DingTalk.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.DingTalk;

/// <summary>
/// 曦寒框架机器人钉钉模块
/// </summary>
[DependsOn(
    typeof(XiHanBotModule)
    )]
public class XiHanBotDingTalkModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanBotDingTalk();
    }
}
