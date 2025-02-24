#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSettingsModule
// Guid:8d40aebb-a267-4d29-8c55-d8b5723570b7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024-04-23 上午 11:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Providers;

namespace XiHan.Framework.Settings;

/// <summary>
/// 曦寒框架设置模块
/// </summary>
public class XiHanSettingsModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        // 配置选项
        _ = services.Configure<SettingOptions>(configuration.GetSection("Settings"));

        _ = services.AddMemoryCache();
        _ = services.AddSingleton<ISettingManager, SettingManager>();

        // 注册默认提供程序
        _ = services.AddTransient<ISettingValueProvider, DefaultValueSettingValueProvider>();

        // 自动注册其他提供程序
        //services.Scan(scan => scan
        //    .FromAssemblyOf<XiHanSettingsModule>()
        //    .AddClasses(classes => classes.AssignableTo<ISettingValueProvider>())
        //    .As<ISettingValueProvider>()
        //    .WithTransientLifetime());
    }
}
