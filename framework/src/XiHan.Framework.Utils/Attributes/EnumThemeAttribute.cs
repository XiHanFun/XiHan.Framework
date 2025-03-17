#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumThemeAttribute
// Guid:fa2ffc88-e08b-4ae8-b018-842ed538f571
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 20:13:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Utils.Attributes;

/// <summary>
/// 枚举主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field)]
public class EnumThemeAttribute : Attribute
{
    /// <summary>
    /// 主题
    /// </summary>
    public string Theme { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="type"></param>
    public EnumThemeAttribute(ThemeType type)
    {
        Theme = type.ToString().ToLower();
    }
}

/// <summary>
/// 主题类型 default、tertiary、primary、info、success、warning 和 error
/// </summary>
public enum ThemeType
{
    /// <summary>
    /// 默认
    /// </summary>
    [Description("default")]
    Default,

    /// <summary>
    /// 三级
    /// </summary>
    [Description("tertiary")]
    Tertiary,

    /// <summary>
    /// 主要
    /// </summary>
    [Description("primary")]
    Primary,

    /// <summary>
    /// 信息
    /// </summary>
    [Description("info")]
    Info,

    /// <summary>
    /// 成功
    /// </summary>
    [Description("success")]
    Success,

    /// <summary>
    /// 警告
    /// </summary>
    [Description("warning")]
    Warning,

    /// <summary>
    /// 错误
    /// </summary>
    [Description("error")]
    Error
}
