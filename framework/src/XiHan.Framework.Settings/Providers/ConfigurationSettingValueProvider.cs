#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConfigurationSettingValueProvider
// Guid:2739dc1f-798e-4b09-a16e-c76f78ccfbc3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 04:43:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Providers;

/// <summary>
/// 配置设置值提供者
/// </summary>
public class ConfigurationSettingValueProvider : ISettingValueProvider, ITransientDependency
{
    /// <summary>
    /// 配置名称前缀
    /// </summary>
    public const string ConfigurationNamePrefix = "Settings:";

    /// <summary>
    /// 提供者名称
    /// </summary>
    public const string ProviderName = "C";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configuration"></param>
    public ConfigurationSettingValueProvider(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name => ProviderName;

    /// <summary>
    /// 配置
    /// </summary>
    protected IConfiguration Configuration { get; }

    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="setting"></param>
    /// <returns></returns>
    public virtual Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        return Task.FromResult(Configuration[ConfigurationNamePrefix + setting.Name]);
    }

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return Task.FromResult(settings.Select(x => new SettingValue(x.Name, Configuration[ConfigurationNamePrefix + x.Name])).ToList());
    }
}
