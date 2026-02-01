#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SettingValue
// Guid:c3c87536-eb0a-482f-aa7a-a286cfc68d80
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 03:53:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core;

namespace XiHan.Framework.Settings.Definitions;

/// <summary>
/// 设置值
/// </summary>
public class SettingValue : NameValue<string?>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SettingValue()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public SettingValue(string name, string? value)
    {
        Name = name;
        Value = value;
    }
}
