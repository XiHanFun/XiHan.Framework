#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplatePartialManager
// Guid:1s6u0u5q-9t2v-0s6u-6u1u-0q5t2v9s6u1u
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板片段管理器实现
/// </summary>
public class TemplatePartialManager : ITemplatePartialManager
{
    private readonly ConcurrentDictionary<string, string> _partials = new();
    private readonly ITemplateEngineRegistry _engineRegistry;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="engineRegistry">模板引擎注册表</param>
    public TemplatePartialManager(ITemplateEngineRegistry engineRegistry)
    {
        _engineRegistry = engineRegistry;
    }

    /// <summary>
    /// 注册模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    public void RegisterPartial(string name, string template)
    {
        _partials[name] = template;
    }

    /// <summary>
    /// 获取模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    public string? GetPartial(string name)
    {
        return _partials.TryGetValue(name, out var partial) ? partial : null;
    }

    /// <summary>
    /// 异步获取模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    public Task<string?> GetPartialAsync(string name)
    {
        var partial = GetPartial(name);
        return Task.FromResult(partial);
    }

    /// <summary>
    /// 移除模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否成功移除</returns>
    public bool RemovePartial(string name)
    {
        return _partials.TryRemove(name, out _);
    }

    /// <summary>
    /// 获取所有片段名称
    /// </summary>
    /// <returns>片段名称集合</returns>
    public IEnumerable<string> GetPartialNames()
    {
        return _partials.Keys;
    }

    /// <summary>
    /// 渲染模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderPartialAsync(string name, ITemplateContext context)
    {
        var partial = GetPartial(name) ?? throw new InvalidOperationException($"找不到模板片段: {name}");
        var engine = _engineRegistry.GetDefaultEngine<string>() ?? throw new InvalidOperationException("没有找到可用的模板引擎");
        return await engine.RenderAsync(partial, context);
    }

    /// <summary>
    /// 预编译所有片段
    /// </summary>
    /// <returns>预编译任务</returns>
    public Task PrecompileAllPartialsAsync()
    {
        // 这里可以实现片段的预编译逻辑
        // 目前是基础实现，返回完成的任务
        return Task.CompletedTask;
    }

    /// <summary>
    /// 清空片段缓存
    /// </summary>
    public void ClearPartialCache()
    {
        _partials.Clear();
    }
}
