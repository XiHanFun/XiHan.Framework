#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLocalizationOptions
// Guid:7f123456-1a2b-3c4d-5e6f-7890abcdef12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/01 10:40:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Localization.Options;

/// <summary>
/// 曦寒本地化选项配置
/// </summary>
public class XiHanLocalizationOptions
{
    /// <summary>
    /// 默认语言，默认为 "zh-CN"
    /// </summary>
    public string DefaultCulture { get; set; } = "zh-CN";

    /// <summary>
    /// 支持的语言列表
    /// </summary>
    public List<string> SupportedCultures { get; set; } = ["zh-CN", "en-US"];

    /// <summary>
    /// 资源路径，默认为 "Localization/Resources"
    /// </summary>
    public string ResourcesPath { get; set; } = "Localization/Resources";

    /// <summary>
    /// 是否启用缓存，默认为 true
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// 缓存生命周期，默认为 30 分钟
    /// </summary>
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 是否回退到父级文化，默认为 true
    /// 例如：如果找不到 "zh-CN" 资源，则尝试查找 "zh" 资源
    /// </summary>
    public bool FallbackToParentCultures { get; set; } = true;

    /// <summary>
    /// 未找到本地化资源时是否抛出异常，默认为 false
    /// </summary>
    public bool ThrowOnMissingResources { get; set; } = false;

    /// <summary>
    /// 文件监控间隔，默认为 30 秒
    /// 设置为 null 表示不监控文件变更
    /// </summary>
    public TimeSpan? FileWatchingInterval { get; set; } = TimeSpan.FromSeconds(30);
}
