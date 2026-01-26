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
using XiHan.Framework.Security;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Settings;

/// <summary>
/// 曦寒框架设置模块
/// </summary>
[DependsOn(
    typeof(XiHanSecurityModule)
    )]
public class XiHanSettingsModule : XiHanModule
{
    /// <summary>
    /// 预配置服务
    /// </summary>
    /// <param name="context"></param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AutoAddDefinitionProviders(context.Services);
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();
        Configure<XiHanSettingOptions>(options =>
        {
            options.ValueProviders.Add<DefaultValueSettingValueProvider>();
            options.ValueProviders.Add<ConfigurationSettingValueProvider>();
            options.ValueProviders.Add<GlobalSettingValueProvider>();
            options.ValueProviders.Add<UserSettingValueProvider>();
        });
    }

    private static void AutoAddDefinitionProviders(IServiceCollection services)
    {
        var definitionProviders = new List<Type>();

        services.OnRegistered(context =>
        {
            if (typeof(ISettingDefinitionProvider).IsAssignableFrom(context.ImplementationType))
            {
                definitionProviders.Add(context.ImplementationType);
            }
        });

        services.Configure<XiHanSettingOptions>(options =>
        {
            options.DefinitionProviders.AddIfNotContains(definitionProviders);
        });
    }
}
