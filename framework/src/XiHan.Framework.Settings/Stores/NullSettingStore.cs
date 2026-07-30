// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

    /// <summary>
    /// 设置值（空实现）
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <param name="providerName"></param>
    /// <param name="providerKey"></param>
    /// <returns></returns>
    public Task SetAsync(string name, string? value, string? providerName, string? providerKey)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除设置值（空实现）
    /// </summary>
    /// <param name="name"></param>
    /// <param name="providerName"></param>
    /// <param name="providerKey"></param>
    /// <returns></returns>
    public Task DeleteAsync(string name, string? providerName, string? providerKey)
    {
        return Task.CompletedTask;
    }
}
