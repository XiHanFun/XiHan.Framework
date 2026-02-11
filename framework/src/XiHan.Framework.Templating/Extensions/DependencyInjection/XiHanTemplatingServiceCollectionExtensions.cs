#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTemplatingServiceCollectionExtensions
// Guid:8eb2d44d-4e2e-49e9-887e-b59e940c2b19
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/06 04:57:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scriban;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;
using XiHan.Framework.Templating.Inheritances;
using XiHan.Framework.Templating.Security;
using XiHan.Framework.Templating.Services;

namespace XiHan.Framework.Templating.Extensions.DependencyInjection;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class XiHanTemplatingServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒框架对象映射服务
    /// </summary>
    /// <param name="services"></param>
    public static IServiceCollection AddXiHanTemplating(this IServiceCollection services)
    {
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

        return services;
    }
}
