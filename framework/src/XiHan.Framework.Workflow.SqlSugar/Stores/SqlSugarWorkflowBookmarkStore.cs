// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Mapping;

namespace XiHan.Framework.Workflow.SqlSugar.Stores;

/// <summary>
/// SqlSugar 流程书签存储
/// </summary>
/// <remarks>
/// 按种类与索引键匹配的查询在数据库条件之后再按序数比较过滤，匹配区分大小写。
/// 按标识删除时未删到任何行即抛出 <see cref="WorkflowException"/>。
/// </remarks>
public class SqlSugarWorkflowBookmarkStore : IWorkflowBookmarkStore
{
    private readonly WorkflowSqlSugarExecutor _executor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="executor">工作流存储的数据库执行器</param>
    public SqlSugarWorkflowBookmarkStore(WorkflowSqlSugarExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>
    /// 按标识查找书签
    /// </summary>
    /// <param name="id">书签标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签（不存在返回 null）</returns>
    public async Task<WorkflowBookmark?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowBookmark>()
                .Where(item => item.BasicId == id)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return entities.Count == 0 ? null : WorkflowBookmarkMapper.ToBookmark(entities[0]);
    }

    /// <summary>
    /// 获取实例的全部书签
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    public async Task<List<WorkflowBookmark>> GetByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowBookmark>()
                .Where(item => item.InstanceId == instanceId)
                .OrderBy(item => item.CreationTime)
                .OrderBy(item => item.BasicId)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return [.. entities.Select(WorkflowBookmarkMapper.ToBookmark)];
    }

    /// <summary>
    /// 获取节点实例的全部书签
    /// </summary>
    /// <param name="nodeInstanceId">节点实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    public async Task<List<WorkflowBookmark>> GetByNodeInstanceAsync(string nodeInstanceId, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowBookmark>()
                .Where(item => item.NodeInstanceId == nodeInstanceId)
                .OrderBy(item => item.CreationTime)
                .OrderBy(item => item.BasicId)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return [.. entities.Select(WorkflowBookmarkMapper.ToBookmark)];
    }

    /// <summary>
    /// 获取到期的定时类书签
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <param name="maxResultCount">最大返回条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>到期书签列表（按到期时间升序）</returns>
    public async Task<List<WorkflowBookmark>> GetDueAsync(DateTime now, int maxResultCount, CancellationToken cancellationToken = default)
    {
        if (maxResultCount <= 0)
        {
            return [];
        }

        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowBookmark>()
                .Where(item => item.DueTime != null && item.DueTime <= now)
                .OrderBy(item => item.DueTime)
                .OrderBy(item => item.BasicId)
                .Take(maxResultCount)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return [.. entities.Select(WorkflowBookmarkMapper.ToBookmark)];
    }

    /// <summary>
    /// 按种类和索引键查询书签
    /// </summary>
    /// <param name="kind">书签种类</param>
    /// <param name="key">索引键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    public async Task<List<WorkflowBookmark>> GetByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowBookmark>()
                .Where(item => item.Kind == kind && item.BookmarkKey == key)
                .OrderBy(item => item.CreationTime)
                .OrderBy(item => item.BasicId)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return [.. entities
            .Where(item => string.Equals(item.Kind, kind, StringComparison.Ordinal)
                && string.Equals(item.BookmarkKey, key, StringComparison.Ordinal))
            .Select(WorkflowBookmarkMapper.ToBookmark)];
    }

    /// <summary>
    /// 查询匹配信号的书签（相关性为 null 表示广播）
    /// </summary>
    /// <param name="signalName">信号名称</param>
    /// <param name="correlationId">业务相关性标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    public async Task<List<WorkflowBookmark>> GetBySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken = default)
    {
        const string signalKind = WorkflowBookmarkKinds.Signal;

        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowBookmark>()
                .Where(item => item.Kind == signalKind && item.BookmarkKey == signalName)
                .WhereIF(correlationId is not null, item => item.CorrelationId == null || item.CorrelationId == correlationId)
                .OrderBy(item => item.CreationTime)
                .OrderBy(item => item.BasicId)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return [.. entities
            .Where(item => string.Equals(item.Kind, signalKind, StringComparison.Ordinal)
                && string.Equals(item.BookmarkKey, signalName, StringComparison.Ordinal)
                && (correlationId is null
                    || item.CorrelationId is null
                    || string.Equals(item.CorrelationId, correlationId, StringComparison.Ordinal)))
            .Select(WorkflowBookmarkMapper.ToBookmark)];
    }

    /// <summary>
    /// 插入书签
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task InsertAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        var entity = WorkflowBookmarkMapper.ToEntity(bookmark);

        await _executor.ExecuteAsync(
            client => client.Insertable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 更新书签，标识不存在时不做任何事
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task UpdateAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        var entity = WorkflowBookmarkMapper.ToEntity(bookmark);

        await _executor.ExecuteAsync(
            client => client.Updateable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 删除书签，未删到任何行时抛出异常
    /// </summary>
    /// <param name="id">书签标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    /// <exception cref="WorkflowException">书签不存在或已被删除</exception>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var affected = await _executor.ExecuteAsync(
            client => client.Deleteable<SysWorkflowBookmark>()
                .Where(item => item.BasicId == id)
                .ExecuteCommandAsync(cancellationToken),
            cancellationToken);

        if (affected == 0)
        {
            throw new WorkflowException($"书签 {id} 不存在或已被处理");
        }
    }

    /// <summary>
    /// 删除实例的全部书签
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task DeleteByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        await _executor.ExecuteAsync(
            client => client.Deleteable<SysWorkflowBookmark>()
                .Where(item => item.InstanceId == instanceId)
                .ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }
}
