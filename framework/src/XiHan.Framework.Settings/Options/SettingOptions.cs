#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SettingOptions
// Guid:37d97385-094d-4fe9-a77c-f288f5fa728d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 4:04:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Options;

/// <summary>
/// 设置选项
/// </summary>
public class SettingOptions
{
    /// <summary>
    /// 默认作用域
    /// </summary>
    public SettingScope DefaultScope { get; set; } = SettingScope.Application;

    /// <summary>
    /// 自动加载预定义设置
    /// </summary>
    public bool AutoLoadDefinitions { get; set; } = true;

    /// <summary>
    /// 加密选项
    /// </summary>
    public AesOptions Encryption { get; set; } = new();
}
