#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SettingChangedEventArgs
// Guid:5d6e7f8a-9b0c-4d3e-a1d2-f3e4a5b6c7d8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/23 11:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Events;

/// <summary>
/// 设置变更事件参数
/// </summary>
public class SettingChangedEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    /// <param name="scope"></param>
    /// <param name="newValue"></param>
    public SettingChangedEventArgs(string name, SettingScope scope, string? newValue)
    {
        Name = name;
        Scope = scope;
        NewValue = newValue;
    }

    /// <summary>
    /// 设置名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 作用域
    /// </summary>
    public SettingScope Scope { get; }

    /// <summary>
    /// 新值
    /// </summary>
    public string? NewValue { get; }
}
