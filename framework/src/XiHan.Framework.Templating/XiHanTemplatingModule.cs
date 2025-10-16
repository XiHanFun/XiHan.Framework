#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTemplatingModule
// Guid:957b2815-e1a0-4f9e-8023-1e5d68482316
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scriban;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Serialization;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;
using XiHan.Framework.Templating.Inheritances;
using XiHan.Framework.Templating.Security;
using XiHan.Framework.Templating.Services;

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

        // 注册核心服务
        services.TryAddSingleton<ITemplateEngineRegistry, TemplateEngineRegistry>();
        services.TryAddSingleton<ITemplateContextFactory, TemplateContextFactory>();
        services.TryAddTransient<ITemplateVariableResolver, TemplateVariableResolver>();

        // 注册模板引擎
        services.TryAddTransient<ITemplateEngine<Template>, ScribanTemplateEngine>();
        services.TryAddTransient<ITemplateEngine<string>, DefaultTemplateEngine>();

        // 注册上下文访问器
        services.TryAddScoped<ITemplateContextAccessor, TemplateContextAccessor>();

        // 注册模板服务
        services.TryAddScoped<ITemplateService, TemplateService>();

        // 注册模板管理服务
        services.TryAddSingleton<ITemplateInheritanceManager, TemplateInheritanceManager>();
        services.TryAddSingleton<ITemplatePartialManager, TemplatePartialManager>();

        // 注册安全服务
        services.TryAddSingleton<ITemplateSecurityAnalyzer, TemplateSecurityAnalyzer>();
        services.TryAddSingleton<ITemplateSecurityChecker, TemplateSecurityChecker>();

        // 配置默认模板引擎
        services.AddOptions<TemplatingOptions>()
            .Configure(options =>
            {
                options.DefaultEngine = "Scriban";
                options.EnableCaching = true;
                options.CacheExpiration = TimeSpan.FromMinutes(30);
            });
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
