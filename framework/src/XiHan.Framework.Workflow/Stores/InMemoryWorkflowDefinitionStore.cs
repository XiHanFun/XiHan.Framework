#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryWorkflowDefinitionStore
// Guid:20d97b3e-6c15-4f8a-b0d4-92e57a61c8f3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:53:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

    /// <inheritdoc />
    public Task<WorkflowDefinition?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_definitions.GetValueOrDefault(id));
    }

    /// <inheritdoc />
    public Task<WorkflowDefinition?> FindByVersionAsync(string code, int version, CancellationToken cancellationToken = default)
    {
        var definition = _definitions.Values
            .FirstOrDefault(item => item.Code == code && item.Version == version);
        return Task.FromResult(definition);
    }

    /// <inheritdoc />
    public Task<WorkflowDefinition?> FindLatestPublishedAsync(string code, CancellationToken cancellationToken = default)
    {
        var definition = _definitions.Values
            .Where(item => item.Code == code && item.Status == WorkflowDefinitionStatus.Published)
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();
        return Task.FromResult(definition);
    }

    /// <inheritdoc />
    public Task<int> GetMaxVersionAsync(string code, CancellationToken cancellationToken = default)
    {
        var versions = _definitions.Values
            .Where(item => item.Code == code)
            .Select(item => item.Version)
            .ToList();
        return Task.FromResult(versions.Count == 0 ? 0 : versions.Max());
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task InsertAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _definitions[definition.Id] = definition;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _definitions[definition.Id] = definition;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _definitions.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
