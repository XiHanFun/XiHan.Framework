#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanUpgradeServiceCollectionExtensions
// Guid:2a3b9706-0a8f-4b6f-9c31-902268c7f3c1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:29:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Extensions;

/// <summary>
/// 升级服务集合扩展
/// </summary>
public static class XiHanUpgradeServiceCollectionExtensions
{
    /// <summary>
    /// 添加升级引擎
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanUpgrade(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<XiHanUpgradeOptions>(configuration.GetSection(XiHanUpgradeOptions.SectionName));

        services.TryAddSingleton<IUpgradeScriptProvider, FileSystemUpgradeScriptProvider>();
        services.TryAddScoped<IUpgradeStatusService, UpgradeStatusService>();
        services.TryAddScoped<IUpgradeEngine, UpgradeEngine>();
        services.TryAddSingleton<IUpgradeCoordinator, UpgradeCoordinator>();
        services.TryAddSingleton<IUpgradeMaintenanceModeManager, DefaultUpgradeMaintenanceModeManager>();
        services.TryAddSingleton<IUpgradeFileUpdater, NullUpgradeFileUpdater>();
        services.TryAddSingleton<IRollingRestartCoordinator, NullRollingRestartCoordinator>();

        return services;
    }

    /// <summary>
    /// 添加升级脚本提供者
    /// </summary>
    /// <typeparam name="TProvider"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddUpgradeScriptProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IUpgradeScriptProvider
    {
        services.AddSingleton<IUpgradeScriptProvider, TProvider>();
        return services;
    }
}
