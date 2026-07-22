// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Scriban;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Serialization;
using XiHan.Framework.Templating.Engines;
using XiHan.Framework.Templating.Extensions.DependencyInjection;

namespace XiHan.Framework.Templating;

/// <summary>
/// 曦寒框架模板引擎模块
/// </summary>
[DependsOn(
    typeof(XiHanSerializationModule)
)]
public class XiHanTemplatingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanTemplating();
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var serviceProvider = context.ServiceProvider;
        var registry = serviceProvider.GetRequiredService<ITemplateEngineRegistry>();

        // 注册默认引擎
        var scribanEngine = serviceProvider.GetRequiredService<ITemplateEngine<Template>>();
        var stringEngine = serviceProvider.GetRequiredService<ITemplateEngine<string>>();

        registry.RegisterEngine("Scriban", scribanEngine);
        registry.RegisterEngine("String", stringEngine);

        // 设置默认引擎
        registry.SetDefaultEngine<Template>("Scriban");
        registry.SetDefaultEngine<string>("String");
    }
}
