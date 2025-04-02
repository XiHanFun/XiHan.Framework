#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ThemeAttribute
// Guid:fa2ffc88-e08b-4ae8-b018-842ed538f571
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 20:13:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.Utils.Attributes;

/// <summary>
/// 主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field)]
public class ThemeAttribute : Attribute
{
    // 缓存
    private static readonly ConcurrentDictionary<ThemeType, ThemeColor> ThemeColorsCatch = new()
    {
        [ThemeType.Default] = new() { Theme = "default", Color = "#35495E" },
        [ThemeType.Tertiary] = new() { Theme = "tertiary", Color = "#697882" },
        [ThemeType.Primary] = new() { Theme = "primary", Color = "#3B86FF" },
        [ThemeType.Info] = new() { Theme = "info", Color = "#FFFFFF00" },
        [ThemeType.Success] = new() { Theme = "success", Color = "#19BE6B" },
        [ThemeType.Warning] = new() { Theme = "warning", Color = "#FF9900" },
        [ThemeType.Error] = new() { Theme = "error", Color = "#ED4014" }
    };

    /// <summary>
    /// 主题颜色
    /// </summary>
    public ThemeColor ThemeColor { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="type"></param>
    public ThemeAttribute(ThemeType type)
    {
        ThemeColor = ThemeColorsCatch[type];
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

/// <summary>
/// 主题颜色
/// </summary>
public record ThemeColor
{
    /// <summary>
    /// 主题
    /// </summary>
    public string Theme { get; set; } = string.Empty;

    /// <summary>
    /// 颜色
    /// </summary>
    public string Color { get; set; } = string.Empty;
}
