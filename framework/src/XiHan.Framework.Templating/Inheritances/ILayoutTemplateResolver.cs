#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILayoutTemplateResolver
// Guid:542aac5a-678f-46a6-becd-b4d7207266c4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:10:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 布局模板解析器
/// </summary>
public interface ILayoutTemplateResolver
{
    /// <summary>
    /// 解析布局模板
    /// </summary>
    /// <param name="layoutName">布局名称</param>
    /// <returns>布局模板</returns>
    Task<string?> ResolveLayoutAsync(string layoutName);

    /// <summary>
    /// 解析布局文件
    /// </summary>
    /// <param name="layoutPath">布局文件路径</param>
    /// <returns>布局模板</returns>
    Task<string?> ResolveLayoutFileAsync(string layoutPath);

    /// <summary>
    /// 缓存布局模板
    /// </summary>
    /// <param name="layoutName">布局名称</param>
    /// <param name="template">布局模板</param>
    void CacheLayout(string layoutName, string template);

    /// <summary>
    /// 清空布局缓存
    /// </summary>
    void ClearLayoutCache();
}
