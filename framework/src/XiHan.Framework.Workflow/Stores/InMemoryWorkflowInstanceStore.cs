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

    /// <summary>
    /// 按标识查找实例
    /// </summary>
    /// <param name="id">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例（不存在返回 null）</returns>
    public Task<WorkflowInstance?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_instances.GetValueOrDefault(id));
    }

    /// <summary>
    /// 查询实例列表
    /// </summary>
    /// <param name="status">状态（为空表示不过滤）</param>
    /// <param name="definitionCode">定义编码（为空表示不过滤）</param>
    /// <param name="correlationId">业务相关性标识（为空表示不过滤）</param>
    /// <param name="maxResultCount">最大返回条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例列表（按创建时间降序）</returns>
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

    /// <summary>
    /// 获取实例的直接子实例列表
    /// </summary>
    /// <param name="parentInstanceId">父实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>子实例列表（按创建时间升序）</returns>
    public Task<List<WorkflowInstance>> GetChildrenAsync(string parentInstanceId, CancellationToken cancellationToken = default)
    {
        var list = _instances.Values
            .Where(item => item.ParentInstanceId == parentInstanceId)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>
    /// 插入实例
    /// </summary>
    /// <param name="instance">实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task InsertAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        _instances[instance.Id] = instance;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新实例
    /// </summary>
    /// <param name="instance">实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        _instances[instance.Id] = instance;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除实例（级联删除节点实例）
    /// </summary>
    /// <param name="id">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _instances.TryRemove(id, out _);

        foreach (var nodeInstance in _nodeInstances.Values.Where(item => item.InstanceId == id).ToList())
        {
            _nodeInstances.TryRemove(nodeInstance.Id, out _);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 按标识查找节点实例
    /// </summary>
    /// <param name="id">节点实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点实例（不存在返回 null）</returns>
    public Task<WorkflowNodeInstance?> FindNodeInstanceAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_nodeInstances.GetValueOrDefault(id));
    }

    /// <summary>
    /// 获取实例的节点实例列表（执行历史）
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点实例列表（按开始时间升序，同刻按创建先后）</returns>
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

    /// <summary>
    /// 插入节点实例
    /// </summary>
    /// <param name="nodeInstance">节点实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task InsertNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default)
    {
        _nodeInstances[nodeInstance.Id] = nodeInstance;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新节点实例
    /// </summary>
    /// <param name="nodeInstance">节点实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task UpdateNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default)
    {
        _nodeInstances[nodeInstance.Id] = nodeInstance;
        return Task.CompletedTask;
    }
}
