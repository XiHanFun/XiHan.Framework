#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSettingsServiceCollectionExtensions
// Guid:42892b6b-ddf7-477f-9eb5-bc5dafe870eb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Providers;

namespace XiHan.Framework.Settings.Extensions.DependencyInjection;

/// <summary>
/// 曦寒设置服务集合扩展
/// </summary>
public static class XiHanSettingsServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒设置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<XiHanSettingOptions>(options =>
        {
            options.ValueProviders.Add<DefaultValueSettingValueProvider>();
            options.ValueProviders.Add<ConfigurationSettingValueProvider>();
            options.ValueProviders.Add<GlobalSettingValueProvider>();
            options.ValueProviders.Add<UserSettingValueProvider>();
        });

        services.Configure<XiHanAesOptions>(configuration.GetSection(XiHanAesOptions.SectionName));

        return services;
    }
}
