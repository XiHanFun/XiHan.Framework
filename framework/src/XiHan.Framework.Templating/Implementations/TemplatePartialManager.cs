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
using XiHan.Framework.Templating.Abstractions;

namespace XiHan.Framework.Templating.Implementations;

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

/// <summary>
/// 内存片段提供者实现
/// </summary>
public class MemoryPartialProvider : IMemoryPartialProvider
{
    private readonly ConcurrentDictionary<string, string> _partials = new();

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string Name => "Memory";

    /// <summary>
    /// 优先级
    /// </summary>
    public int Priority => 0;

    /// <summary>
    /// 是否支持片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否支持</returns>
    public bool SupportsPartial(string name)
    {
        return _partials.ContainsKey(name);
    }

    /// <summary>
    /// 获取片段模板
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    public Task<string?> GetPartialTemplateAsync(string name)
    {
        var template = _partials.TryGetValue(name, out var partial) ? partial : null;
        return Task.FromResult(template);
    }

    /// <summary>
    /// 获取片段信息
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段信息</returns>
    public Task<PartialTemplateInfo?> GetPartialInfoAsync(string name)
    {
        if (!_partials.TryGetValue(name, out var template))
        {
            return Task.FromResult<PartialTemplateInfo?>(null);
        }

        var info = new PartialTemplateInfo
        {
            Name = name,
            Path = null,
            LastModified = DateTime.UtcNow,
            Size = template?.Length ?? 0,
            ContentHash = template?.GetHashCode().ToString()
        };

        return Task.FromResult<PartialTemplateInfo?>(info);
    }

    /// <summary>
    /// 监听片段变化
    /// </summary>
    /// <param name="callback">变化回调</param>
    /// <returns>监听器</returns>
    public IDisposable? WatchChanges(Action<PartialTemplateChangeEvent> callback)
    {
        // 内存提供者不支持变化监听
        return null;
    }

    /// <summary>
    /// 添加片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    public void AddPartial(string name, string template)
    {
        _partials[name] = template;
    }

    /// <summary>
    /// 更新片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    public void UpdatePartial(string name, string template)
    {
        _partials[name] = template;
    }

    /// <summary>
    /// 移除片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否成功移除</returns>
    public bool RemovePartial(string name)
    {
        return _partials.TryRemove(name, out _);
    }

    /// <summary>
    /// 清空片段
    /// </summary>
    public void ClearPartials()
    {
        _partials.Clear();
    }
}
