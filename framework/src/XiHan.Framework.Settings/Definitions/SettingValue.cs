// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
