#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullSettingStore
// Guid:1a62781a-f9e6-4196-98d4-60d9ca2b2aa3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 4:51:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Stores;

/// <summary>
/// NullSettingStore
/// </summary>
[Dependency(TryRegister = true)]
public class NullSettingStore : ISettingStore, ISingletonDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public NullSettingStore()
    {
        Logger = NullLogger<NullSettingStore>.Instance;
    }

    /// <summary>
    /// 日志接口
    /// </summary>
    public ILogger<NullSettingStore> Logger { get; set; }

    /// <summary>
    /// 获取设置值（如果不存在则返回null）
    /// </summary>
    /// <param name="name"></param>
    /// <param name="providerName"></param>
    /// <param name="providerKey"></param>
    /// <returns></returns>
    public Task<string?> GetOrNullAsync(string name, string? providerName, string? providerKey)
    {
        return Task.FromResult((string?)null);
    }

    /// <summary>
    /// 获取所有设置值（如果不存在则返回null）
    /// </summary>
    /// <param name="names"></param>
    /// <param name="providerName"></param>
    /// <param name="providerKey"></param>
    /// <returns></returns>
    public Task<List<SettingValue>> GetAllAsync(string[] names, string? providerName, string? providerKey)
    {
        return Task.FromResult(names.Select(x => new SettingValue(x, null)).ToList());
    }
}
