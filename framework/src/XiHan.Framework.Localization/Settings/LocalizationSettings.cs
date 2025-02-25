#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalizationSettings
// Guid:9e8d7f6a-5b4c-3e2d-1f0a-9b8c7d6e5f4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 13:18:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Localization.Settings;

/// <summary>
/// 本地化设置常量
/// </summary>
public static class LocalizationSettings
{
    /// <summary>
    /// 设置组名
    /// </summary>
    public const string GroupName = "Localization";

    /// <summary>
    /// 默认语言设置键
    /// </summary>
    public const string DefaultLanguage = GroupName + ".DefaultLanguage";

    /// <summary>
    /// 可用语言设置键
    /// </summary>
    public const string AvailableLanguages = GroupName + ".AvailableLanguages";

    /// <summary>
    /// 语言回退机制设置键
    /// </summary>
    public const string FallbackToDefaultLanguage = GroupName + ".FallbackToDefaultLanguage";

    /// <summary>
    /// 本地化资源缓存时间设置键（分钟）
    /// </summary>
    public const string ResourceCacheMinutes = GroupName + ".ResourceCacheMinutes";
}
