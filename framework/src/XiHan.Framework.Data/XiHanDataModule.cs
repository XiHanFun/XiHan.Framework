// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Auditing;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data.Extensions.DependencyInjection;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.Domain;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Security;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data;

/// <summary>
/// 曦寒数据访问模块
/// 提供基于SqlSugar的数据访问实现，集成领域驱动设计和工作单元模式
/// </summary>
[DependsOn(
    typeof(XiHanDomainModule),
    typeof(XiHanUowModule),
    typeof(XiHanDistributedIdsModule),
    typeof(XiHanMultiTenancyModule),
    typeof(XiHanSecurityModule),
    typeof(XiHanAuditingModule)
    )]
public class XiHanDataModule : XiHanModule
{
    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 添加SqlSugar数据访问服务
        services.AddXiHanDataSqlSugar(config);
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        using var scope = context.ServiceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IDbInitializer>>();

        try
        {
            logger.LogInformation("准备初始化数据库...");

            var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
            await initializer.InitializeAsync();

            logger.LogInformation("数据库初始化成功！");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "数据库初始化失败: {Message}", ex.Message);
            throw;
        }
    }
}
