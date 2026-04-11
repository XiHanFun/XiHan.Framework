#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLocalizationOptions
// Guid:4f191d36-1f73-40dc-8e88-f5d2d95f3816
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Localization.Options;

/// <summary>
/// 本地化配置选项
/// </summary>
public class XiHanLocalizationOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Localization";

    /// <summary>
    /// 本地化资源根目录（虚拟路径）
    /// </summary>
    public string ResourcesPath { get; set; } = "/Localization";

    /// <summary>
    /// 是否启用动态 JSON 重载
    /// </summary>
    public bool EnableDynamicJsonReload { get; set; } = true;

    /// <summary>
    /// 默认资源名称
    /// </summary>
    public string DefaultResourceName { get; set; } = "Default";

    /// <summary>
    /// 枚举本地化默认资源名
    /// </summary>
    public string EnumResourceName { get; set; } = "Enums";

    /// <summary>
    /// 枚举本地化默认键前缀
    /// 空字符串表示不使用前缀
    /// </summary>
    public string EnumLocalizationKeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// 默认文化
    /// </summary>
    public string DefaultCulture { get; set; } = "zh-CN";

    /// <summary>
    /// 是否回退到父文化
    /// </summary>
    public bool FallbackToParentCultures { get; set; } = true;

    /// <summary>
    /// 是否回退到默认文化
    /// </summary>
    public bool FallbackToDefaultCulture { get; set; } = true;
}
