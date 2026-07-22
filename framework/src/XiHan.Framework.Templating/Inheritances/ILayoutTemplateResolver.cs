// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
