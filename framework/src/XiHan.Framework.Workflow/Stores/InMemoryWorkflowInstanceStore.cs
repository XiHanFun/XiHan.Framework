// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;

namespace XiHan.Framework.Workflow.Stores;

/// <summary>
/// 内存流程实例存储（进程内单实例场景的默认实现）
/// </summary>
public class InMemoryWorkflowInstanceStore : IWorkflowInstanceStore
{
    private readonly ConcurrentDictionary<string, WorkflowInstance> _instances = new();
    private readonly ConcurrentDictionary<string, WorkflowNodeInstance> _nodeInstances = new();

    /// <inheritdoc />
    public Task<WorkflowInstance?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_instances.GetValueOrDefault(id));
    }

    /// <inheritdoc />
    public Task<List<WorkflowInstance>> GetListAsync(
        WorkflowInstanceStatus? status = null,
        string? definitionCode = null,
        string? correlationId = null,
        int maxResultCount = 100,
        CancellationToken cancellationToken = default)
    {
        var list = _instances.Values
            .Where(item => status is null || item.Status == status)
            .Where(item => definitionCode is null || item.DefinitionCode == definitionCode)
            .Where(item => correlationId is null || item.CorrelationId == correlationId)
            .OrderByDescending(item => item.CreationTime)
            .Take(maxResultCount)
            .ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<List<WorkflowInstance>> GetChildrenAsync(string parentInstanceId, CancellationToken cancellationToken = default)
    {
        var list = _instances.Values
            .Where(item => item.ParentInstanceId == parentInstanceId)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task InsertAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        _instances[instance.Id] = instance;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        _instances[instance.Id] = instance;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _instances.TryRemove(id, out _);

        foreach (var nodeInstance in _nodeInstances.Values.Where(item => item.InstanceId == id).ToList())
        {
            _nodeInstances.TryRemove(nodeInstance.Id, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowNodeInstance?> FindNodeInstanceAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_nodeInstances.GetValueOrDefault(id));
    }

    /// <inheritdoc />
    public Task<List<WorkflowNodeInstance>> GetNodeInstancesAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        // 同刻开始的节点按雪花标识回溯创建先后，保证执行历史顺序稳定
        var list = _nodeInstances.Values
            .Where(item => item.InstanceId == instanceId)
            .OrderBy(item => item.StartTime)
            .ThenBy(item => long.TryParse(item.Id, out var order) ? order : 0)
            .ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task InsertNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default)
    {
        _nodeInstances[nodeInstance.Id] = nodeInstance;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default)
    {
        _nodeInstances[nodeInstance.Id] = nodeInstance;
        return Task.CompletedTask;
    }
}
