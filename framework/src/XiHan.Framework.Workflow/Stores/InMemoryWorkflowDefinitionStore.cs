// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Stores;

namespace XiHan.Framework.Workflow.Stores;

/// <summary>
/// 内存流程定义存储（进程内单实例场景的默认实现）
/// </summary>
public class InMemoryWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    private readonly ConcurrentDictionary<string, WorkflowDefinition> _definitions = new();

    /// <summary>
    /// 按标识查找定义
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    public Task<WorkflowDefinition?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_definitions.GetValueOrDefault(id));
    }

    /// <summary>
    /// 按编码和版本查找定义
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="version">版本号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    public Task<WorkflowDefinition?> FindByVersionAsync(string code, int version, CancellationToken cancellationToken = default)
    {
        var definition = _definitions.Values
            .FirstOrDefault(item => item.Code == code && item.Version == version);
        return Task.FromResult(definition);
    }

    /// <summary>
    /// 查找编码下最新的已发布定义
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    public Task<WorkflowDefinition?> FindLatestPublishedAsync(string code, CancellationToken cancellationToken = default)
    {
        var definition = _definitions.Values
            .Where(item => item.Code == code && item.Status == WorkflowDefinitionStatus.Published)
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();
        return Task.FromResult(definition);
    }

    /// <summary>
    /// 获取编码下的最大版本号
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最大版本号（编码不存在返回 0）</returns>
    public Task<int> GetMaxVersionAsync(string code, CancellationToken cancellationToken = default)
    {
        var versions = _definitions.Values
            .Where(item => item.Code == code)
            .Select(item => item.Version)
            .ToList();
        return Task.FromResult(versions.Count == 0 ? 0 : versions.Max());
    }

    /// <summary>
    /// 查询定义列表
    /// </summary>
    /// <param name="code">流程编码（为空表示不过滤）</param>
    /// <param name="status">状态（为空表示不过滤）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义列表（按编码升序、版本降序）</returns>
    public Task<List<WorkflowDefinition>> GetListAsync(
        string? code = null,
        WorkflowDefinitionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var list = _definitions.Values
            .Where(item => code is null || item.Code == code)
            .Where(item => status is null || item.Status == status)
            .OrderBy(item => item.Code)
            .ThenByDescending(item => item.Version)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>
    /// 插入定义
    /// </summary>
    /// <param name="definition">定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task InsertAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _definitions[definition.Id] = definition;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新定义
    /// </summary>
    /// <param name="definition">定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _definitions[definition.Id] = definition;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除定义
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _definitions.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
