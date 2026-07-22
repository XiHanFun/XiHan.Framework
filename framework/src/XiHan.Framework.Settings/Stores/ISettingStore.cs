// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Stores;

/// <summary>
/// 设置存储接口
/// </summary>
public interface ISettingStore
{
    /// <summary>
    /// 获取设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>设置值</returns>
    Task<string?> GetOrNullAsync(string name, string? providerName, string? providerKey);

    /// <summary>
    /// 获取所有设置值
    /// </summary>
    /// <param name="names">设置名称数组</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns>设置值列表</returns>
    Task<List<SettingValue>> GetAllAsync(string[] names, string? providerName, string? providerKey);

    /// <summary>
    /// 设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="value">设置值</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns></returns>
    Task SetAsync(string name, string? value, string? providerName, string? providerKey);

    /// <summary>
    /// 删除设置值
    /// </summary>
    /// <param name="name">设置名称</param>
    /// <param name="providerName">提供者名称</param>
    /// <param name="providerKey">提供者键</param>
    /// <returns></returns>
    Task DeleteAsync(string name, string? providerName, string? providerKey);
}
