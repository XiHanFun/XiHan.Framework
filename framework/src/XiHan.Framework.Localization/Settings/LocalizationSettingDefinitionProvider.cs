#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalizationSettingDefinitionProvider
// Guid:5c4a9e8d-7f6a-5b4c-3e2d-1f0a9b8c7d6e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:38:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Localization.Settings;

/// <summary>
/// 本地化设置定义提供者
/// </summary>
public class LocalizationSettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <summary>
    /// 定义设置
    /// </summary>
    /// <param name="context">设置定义上下文</param>
    public void Define(ISettingDefinitionContext context)
    {
        // 默认语言设置
        context.Add(new SettingDefinition(
            LocalizationSettings.DefaultLanguage,
            "en",
            "默认语言",
            "应用程序默认语言",
            LocalizationSettings.GroupName
        ));

        // 可用语言设置（逗号分隔的语言代码列表）
        context.Add(new SettingDefinition(
            LocalizationSettings.AvailableLanguages,
            "en,zh-CN",
            "可用语言",
            "应用程序支持的语言列表（逗号分隔）",
            LocalizationSettings.GroupName,
            validator: value => !string.IsNullOrWhiteSpace(value)
        ));

        // 回退到默认语言
        context.Add(new SettingDefinition(
            LocalizationSettings.FallbackToDefaultLanguage,
            "true",
            "回退到默认语言",
            "当请求的语言不可用时，是否回退到默认语言",
            LocalizationSettings.GroupName
        ));

        // 本地化资源缓存时间（分钟）
        context.Add(new SettingDefinition(
            LocalizationSettings.ResourceCacheMinutes,
            "30",
            "资源缓存时间",
            "本地化资源缓存有效时间（分钟）",
            LocalizationSettings.GroupName,
            validator: value => int.TryParse(value, out var minutes) && minutes >= 0
        ));
    }
}
