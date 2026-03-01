#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanUpgradeModule
// Guid:8b2b3b1b-4b3d-4d86-8e7d-1b9f7a6d5e2c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 17:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Extensions;
using XiHan.Framework.Upgrade.Options;

namespace XiHan.Framework.Upgrade;

/// <summary>
/// 曦寒框架升级模块
/// </summary>
public class XiHanUpgradeModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanUpgrade(config);
    }

    /// <summary>
    /// 应用初始化后自动检查升级状态
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        using var scope = context.ServiceProvider.CreateScope();
        var options = scope.ServiceProvider.GetService<IOptions<XiHanUpgradeOptions>>()?.Value;
        if (options != null && !options.EnableAutoCheckOnStartup)
        {
            return;
        }

        var versionStore = scope.ServiceProvider.GetService<IUpgradeVersionStore>();
        if (versionStore == null)
        {
            return;
        }

        var statusService = scope.ServiceProvider.GetService<IUpgradeStatusService>();
        if (statusService == null)
        {
            return;
        }

        await statusService.EnsureInitializedAsync();
    }
}
