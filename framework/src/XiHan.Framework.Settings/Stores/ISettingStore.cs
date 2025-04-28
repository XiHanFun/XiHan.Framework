#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISettingStore
// Guid:4c5d0e1e-0b0a-4d2f-9c7d-3d8f1a7b2c1a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024-04-23 上午 11:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    /// <returns>任务</returns>
    Task SetValueAsync(string name, string? value, string? providerName, string? providerKey);
}
