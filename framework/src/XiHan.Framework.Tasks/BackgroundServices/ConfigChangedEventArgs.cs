// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 配置变更事件参数
/// </summary>
public class ConfigChangedEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="propertyName">配置项名称</param>
    /// <param name="oldValue">旧值</param>
    /// <param name="newValue">新值</param>
    public ConfigChangedEventArgs(string propertyName, object? oldValue, object? newValue)
    {
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>
    /// 变更的配置项名称
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// 旧值
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// 新值
    /// </summary>
    public object? NewValue { get; }
}
