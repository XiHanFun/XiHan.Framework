#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ThemeType
// Guid:0a12b528-1de2-4e23-bf99-6b7e4428bd98
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/17 3:29:48
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Utils.Themes;

/// <summary>
/// 主题类型
/// </summary>
public enum ThemeType
{
    /// <summary>
    /// 默认主题
    /// </summary>
    [Description("默认主题")]
    Default,

    /// <summary>
    /// 主要主题
    /// </summary>
    [Description("主要主题")]
    Primary,

    /// <summary>
    /// 次要主题
    /// </summary>
    [Description("次要主题")]
    Secondary,

    /// <summary>
    /// 三级主题
    /// </summary>
    [Description("三级主题")]
    Tertiary,

    /// <summary>
    /// 信息主题
    /// </summary>
    [Description("信息主题")]
    Info,

    /// <summary>
    /// 成功主题
    /// </summary>
    [Description("成功主题")]
    Success,

    /// <summary>
    /// 警告主题
    /// </summary>
    [Description("警告主题")]
    Warning,

    /// <summary>
    /// 错误主题
    /// </summary>
    [Description("错误主题")]
    Error,

    /// <summary>
    /// 浅色主题
    /// </summary>
    [Description("浅色主题")]
    Light,

    /// <summary>
    /// 深色主题
    /// </summary>
    [Description("深色主题")]
    Dark,

    /// <summary>
    /// 静音主题
    /// </summary>
    [Description("静音主题")]
    Muted,

    /// <summary>
    /// 链接主题
    /// </summary>
    [Description("链接主题")]
    Link,

    /// <summary>
    /// 透明主题
    /// </summary>
    [Description("透明主题")]
    Transparent,

    /// <summary>
    /// 自定义主题
    /// </summary>
    [Description("自定义主题")]
    Custom
}
