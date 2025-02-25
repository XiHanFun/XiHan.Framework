#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IResourceStringProvider
// Guid:8c9d7f6a-5b4c-3e2d-1f0a-9b8c7d6e5f4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 13:15:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Resources;

/// <summary>
/// 资源字符串提供者接口
/// </summary>
public interface IResourceStringProvider
{
    /// <summary>
    /// 获取指定名称的本地化字符串
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <param name="name">资源名称</param>
    /// <param name="cultureName">文化名称</param>
    /// <returns>本地化字符串</returns>
    string? GetString(ILocalizationResource resource, string name, string cultureName);

    /// <summary>
    /// 获取所有本地化字符串
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <param name="cultureName">文化名称</param>
    /// <param name="includeParentCultures">是否包含父级文化</param>
    /// <returns>本地化字符串集合</returns>
    IEnumerable<LocalizedString> GetAllStrings(ILocalizationResource resource, string cultureName, bool includeParentCultures);

    /// <summary>
    /// 获取支持的文化列表
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <returns>支持的文化列表</returns>
    IReadOnlyList<string> GetSupportedCultures(ILocalizationResource resource);
}
