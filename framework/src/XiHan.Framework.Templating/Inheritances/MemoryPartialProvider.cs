#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MemoryPartialProvider
// Guid:2e0b7d45-015b-4da7-9039-ff20609fefc8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Templating.Inheritances;

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
