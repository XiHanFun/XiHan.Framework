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

namespace XiHan.Framework.Settings.Stores;

/// <summary>
/// 设置存储接口
/// </summary>
public interface ISettingStore
{
    /// <summary>
    /// 获取设置值
    /// </summary>
    Task<string?> GetOrNullAsync(string name, SettingScope scope);

    /// <summary>
    /// 设置值
    /// </summary>
    Task SetValueAsync(string name, string? value, SettingScope scope);
}
