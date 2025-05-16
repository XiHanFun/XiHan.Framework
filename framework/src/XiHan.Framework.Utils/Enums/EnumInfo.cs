#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumInfo
// Guid:eb3bf937-4be6-405e-9885-4292db553217
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/17 3:30:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 枚举信息
/// </summary>
public record EnumInfo : ThemeColor
{
    /// <summary>
    /// 键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 值
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Label { get; init; } = string.Empty;
}

/// <summary>
/// 主题颜色
/// </summary>
public record ThemeColor
{
    /// <summary>
    /// 主题
    /// </summary>
    public string Theme { get; set; } = "default";

    /// <summary>
    /// 颜色
    /// </summary>
    public string Color { get; set; } = "#35495E";
}
