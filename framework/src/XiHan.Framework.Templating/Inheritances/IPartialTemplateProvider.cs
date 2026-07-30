// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板片段提供者
/// </summary>
public interface IPartialTemplateProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 优先级
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 是否支持片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否支持</returns>
    bool SupportsPartial(string name);

    /// <summary>
    /// 获取片段模板
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    Task<string?> GetPartialTemplateAsync(string name);

    /// <summary>
    /// 获取片段信息
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段信息</returns>
    Task<PartialTemplateInfo?> GetPartialInfoAsync(string name);

    /// <summary>
    /// 监听片段变化
    /// </summary>
    /// <param name="callback">变化回调</param>
    /// <returns>监听器</returns>
    IDisposable? WatchChanges(Action<PartialTemplateChangeEvent> callback);
}
