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

using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 主题类型 default、tertiary、primary、info、success、warning 和 error
/// </summary>
public enum ThemeType
{
    /// <summary>
    /// 默认
    /// </summary>
    [Display(Name = "default", Description = "#35495E")]
    Default,

    /// <summary>
    /// 三级
    /// </summary>
    [Display(Name = "tertiary", Description = "#697882")]
    Tertiary,

    /// <summary>
    /// 主要
    /// </summary>
    [Display(Name = "primary", Description = "#3B86FF")]
    Primary,

    /// <summary>
    /// 信息
    /// </summary>
    [Display(Name = "info", Description = "#FFFFFF00")]
    Info,

    /// <summary>
    /// 成功
    /// </summary>
    [Display(Name = "success", Description = "#19BE6B")]
    Success,

    /// <summary>
    /// 警告
    /// </summary>
    [Display(Name = "warning", Description = "#FF9900")]
    Warning,

    /// <summary>
    /// 错误
    /// </summary>
    [Display(Name = "error", Description = "#ED4014")]
    Error
}
