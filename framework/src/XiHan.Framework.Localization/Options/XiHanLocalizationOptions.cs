#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLocalizationOptions
// Guid:c9c492d0-2c70-4814-b9d3-618ce3aa9b92
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 5:05:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Localization.Options;

/// <summary>
/// 曦寒本地化选项
/// </summary>
public class XiHanLocalizationOptions
{
    /// <summary>
    /// 默认资源文件夹路径
    /// </summary>
    public string DefaultResourcesPath { get; set; } = "Localization/Resources";

    /// <summary>
    /// 默认语言文化
    /// </summary>
    public string DefaultCulture { get; set; } = "zh-CN";

    /// <summary>
    /// 是否使用资源键作为默认值（当找不到本地化资源时）
    /// </summary>
    public bool ReturnKeyOnNotFound { get; set; } = true;

    /// <summary>
    /// 是否缓存本地化资源
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// 资源文件搜索模式
    /// </summary>
    public string ResourceFileSearchPattern { get; set; } = "*.json";

    /// <summary>
    /// 是否启用资源文件监视（热更新）
    /// </summary>
    public bool EnableFileWatching { get; set; } = true;

    /// <summary>
    /// 支持的语言文化列表
    /// </summary>
    public List<string> SupportedCultures { get; set; } = ["zh-CN", "en-US"];

    /// <summary>
    /// 是否记录丢失的资源键
    /// </summary>
    public bool LogMissingKeys { get; set; } = true;
}
