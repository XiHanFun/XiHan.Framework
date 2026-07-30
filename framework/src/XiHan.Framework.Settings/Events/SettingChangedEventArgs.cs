// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
