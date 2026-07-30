// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板片段注册表
/// </summary>
public interface IPartialTemplateRegistry
{
    /// <summary>
    /// 注册片段提供者
    /// </summary>
    /// <param name="provider">片段提供者</param>
    void RegisterProvider(IPartialTemplateProvider provider);

    /// <summary>
    /// 移除片段提供者
    /// </summary>
    /// <param name="provider">片段提供者</param>
    void RemoveProvider(IPartialTemplateProvider provider);

    /// <summary>
    /// 解析片段模板
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    Task<string?> ResolvePartialAsync(string name);

    /// <summary>
    /// 批量解析片段模板
    /// </summary>
    /// <param name="names">片段名称集合</param>
    /// <returns>片段模板字典</returns>
    Task<IDictionary<string, string>> ResolvePartialsAsync(IEnumerable<string> names);

    /// <summary>
    /// 监听片段变化
    /// </summary>
    /// <param name="callback">变化回调</param>
    /// <returns>监听器</returns>
    IDisposable WatchPartialChanges(Action<PartialTemplateChangeEvent> callback);
}
