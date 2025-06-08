#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHardwareInfoProvider
// Guid:a1b2c3d4-e5f6-7890-a1b2-c3d4e5f67890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2025-01-01 上午 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.HardwareInfos.Abstractions;

/// <summary>
/// 硬件信息提供者基础接口
/// </summary>
/// <typeparam name="T">硬件信息类型</typeparam>
public interface IHardwareInfoProvider<T>
{
    /// <summary>
    /// 获取硬件信息
    /// </summary>
    /// <returns>硬件信息</returns>
    T GetInfo();

    /// <summary>
    /// 异步获取硬件信息
    /// </summary>
    /// <returns>硬件信息</returns>
    Task<T> GetInfoAsync();

    /// <summary>
    /// 获取缓存的硬件信息
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新缓存</param>
    /// <returns>硬件信息</returns>
    T GetCachedInfo(bool forceRefresh = false);

    /// <summary>
    /// 异步获取缓存的硬件信息
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新缓存</param>
    /// <returns>硬件信息</returns>
    Task<T> GetCachedInfoAsync(bool forceRefresh = false);
}

/// <summary>
/// 硬件信息基础接口
/// </summary>
public interface IHardwareInfo
{
    /// <summary>
    /// 获取时间戳
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// 是否可用
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 错误信息
    /// </summary>
    string? ErrorMessage { get; }
}
