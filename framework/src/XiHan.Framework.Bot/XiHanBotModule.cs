// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Http;
using XiHan.Framework.Templating;

namespace XiHan.Framework.Bot;

/// <summary>
/// 曦寒机器人模板模块
/// </summary>
[DependsOn(
    typeof(XiHanHttpModule),
    typeof(XiHanTemplatingModule)
    )]
public class XiHanBotModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanBot();
    }
}
