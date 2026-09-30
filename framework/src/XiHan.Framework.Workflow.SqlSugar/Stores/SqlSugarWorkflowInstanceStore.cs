// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Mapping;

namespace XiHan.Framework.Workflow.SqlSugar.Stores;

/// <summary>
/// SqlSugar 流程实例存储（实例与节点实例执行历史）
/// </summary>
public class SqlSugarWorkflowInstanceStore : IWorkflowInstanceStore
{
    private readonly WorkflowSqlSugarExecutor _executor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="executor">工作流存储的数据库执行器</param>
    public SqlSugarWorkflowInstanceStore(WorkflowSqlSugarExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>
    /// 按标识查找实例
    /// </summary>
    /// <param name="id">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例（不存在返回 null）</returns>
    public async Task<WorkflowInstance?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowInstance>()
                .Where(item => item.BasicId == id)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return entities.Count == 0 ? null : WorkflowInstanceMapper.ToInstance(entities[0]);
    }

    /// <summary>
    /// 查询实例列表
    /// </summary>
    /// <param name="status">状态（为空表示不过滤）</param>
    /// <param name="definitionCode">定义编码（为空表示不过滤）</param>
    /// <param name="correlationId">业务相关性标识（为空表示不过滤）</param>
    /// <param name="maxResultCount">最大返回条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实例列表（按创建时间降序；指定定义编码时按页读取候选，在内存中按序数比较过滤，凑满条数或读完所有页后停止）</returns>
    public async Task<List<WorkflowInstance>> GetListAsync(
        WorkflowInstanceStatus? status = null,
        string? definitionCode = null,
        string? correlationId = null,
        int maxResultCount = 100,
        CancellationToken cancellationToken = default)
    {
        if (maxResultCount <= 0)
        {
            return [];
        }

        var statusValue = (int)(status ?? default);

        var entities = await _executor.ExecuteAsync(
            async client =>
            {
                var query = client.Queryable<SysWorkflowInstance>()
                    .WhereIF(status is not null, item => item.Status == statusValue)
                    .WhereIF(definitionCode is not null, item => item.DefinitionCode == definitionCode)
                    .WhereIF(correlationId is not null, item => item.CorrelationId == correlationId)
                    .OrderBy(item => item.CreationTime, OrderByType.Desc)
                    .OrderBy(item => item.BasicId, OrderByType.Desc);

                if (definitionCode is null)
                {
                    return await query.Take(maxResultCount).ToListAsync(cancellationToken);
                }

                var matched = new List<SysWorkflowInstance>(maxResultCount);
                for (var pageIndex = 1; matched.Count < maxResultCount; pageIndex++)
                {
                    var page = await query.ToPageListAsync(pageIndex, maxResultCount, cancellationToken);
                    matched.AddRange(page
                        .Where(item => string.Equals(item.DefinitionCode, definitionCode, StringComparison.Ordinal))
                        .Take(maxResultCount - matched.Count));

                    if (page.Count < maxResultCount)
                    {
                        break;
                    }
                }

                return matched;
            },
            cancellationToken);

        return [.. entities.Select(WorkflowInstanceMapper.ToInstance)];
    }

    /// <summary>
    /// 获取实例的直接子实例列表
    /// </summary>
    /// <param name="parentInstanceId">父实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>子实例列表（按创建时间升序）</returns>
    public async Task<List<WorkflowInstance>> GetChildrenAsync(string parentInstanceId, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowInstance>()
                .Where(item => item.ParentInstanceId == parentInstanceId)
                .OrderBy(item => item.CreationTime)
                .OrderBy(item => item.BasicId)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return [.. entities.Select(WorkflowInstanceMapper.ToInstance)];
    }

    /// <summary>
    /// 插入实例
    /// </summary>
    /// <param name="instance">实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task InsertAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var entity = WorkflowInstanceMapper.ToEntity(instance);

        await _executor.ExecuteAsync(
            client => client.Insertable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 更新实例，标识不存在时不做任何事
    /// </summary>
    /// <param name="instance">实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var entity = WorkflowInstanceMapper.ToEntity(instance);

        await _executor.ExecuteAsync(
            client => client.Updateable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 在同一事务内删除实例及其全部节点实例
    /// </summary>
    /// <param name="id">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _executor.ExecuteAsync(
            async client =>
            {
                await client.Deleteable<SysWorkflowNodeInstance>()
                    .Where(item => item.InstanceId == id)
                    .ExecuteCommandAsync(cancellationToken);

                return await client.Deleteable<SysWorkflowInstance>()
                    .Where(item => item.BasicId == id)
                    .ExecuteCommandAsync(cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>
    /// 按标识查找节点实例
    /// </summary>
    /// <param name="id">节点实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点实例（不存在返回 null）</returns>
    public async Task<WorkflowNodeInstance?> FindNodeInstanceAsync(string id, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowNodeInstance>()
                .Where(item => item.BasicId == id)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return entities.Count == 0 ? null : WorkflowNodeInstanceMapper.ToNodeInstance(entities[0]);
    }

    /// <summary>
    /// 获取实例的节点实例列表（执行历史）
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点实例列表（按开始时间升序，同刻按创建顺序）</returns>
    public async Task<List<WorkflowNodeInstance>> GetNodeInstancesAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowNodeInstance>()
                .Where(item => item.InstanceId == instanceId)
                .OrderBy(item => item.StartTime)
                .OrderBy(item => item.Sequence)
                .OrderBy(item => item.BasicId)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return [.. entities.Select(WorkflowNodeInstanceMapper.ToNodeInstance)];
    }

    /// <summary>
    /// 插入节点实例
    /// </summary>
    /// <param name="nodeInstance">节点实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task InsertNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default)
    {
        var entity = WorkflowNodeInstanceMapper.ToEntity(nodeInstance);

        await _executor.ExecuteAsync(
            client => client.Insertable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 更新节点实例，标识不存在时不做任何事
    /// </summary>
    /// <param name="nodeInstance">节点实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task UpdateNodeInstanceAsync(WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken = default)
    {
        var entity = WorkflowNodeInstanceMapper.ToEntity(nodeInstance);

        await _executor.ExecuteAsync(
            client => client.Updateable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }
}
