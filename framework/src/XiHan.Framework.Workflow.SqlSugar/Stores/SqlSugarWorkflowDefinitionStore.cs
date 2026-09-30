// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Mapping;

namespace XiHan.Framework.Workflow.SqlSugar.Stores;

/// <summary>
/// SqlSugar 流程定义存储
/// </summary>
public class SqlSugarWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    private readonly WorkflowSqlSugarExecutor _executor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="executor">工作流存储的数据库执行器</param>
    public SqlSugarWorkflowDefinitionStore(WorkflowSqlSugarExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>
    /// 按标识查找定义
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    public async Task<WorkflowDefinition?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowDefinition>()
                .Where(item => item.BasicId == id)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return entities.Count == 0 ? null : WorkflowDefinitionMapper.ToDefinition(entities[0]);
    }

    /// <summary>
    /// 按编码和版本查找定义
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="version">版本号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    public async Task<WorkflowDefinition?> FindByVersionAsync(string code, int version, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowDefinition>()
                .Where(item => item.Code == code && item.Version == version)
                .ToListAsync(cancellationToken),
            cancellationToken);

        var matched = entities.Find(item => string.Equals(item.Code, code, StringComparison.Ordinal));
        return matched is null ? null : WorkflowDefinitionMapper.ToDefinition(matched);
    }

    /// <summary>
    /// 查找编码下最新的已发布定义
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义（不存在返回 null）</returns>
    public async Task<WorkflowDefinition?> FindLatestPublishedAsync(string code, CancellationToken cancellationToken = default)
    {
        const int published = (int)WorkflowDefinitionStatus.Published;

        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowDefinition>()
                .Where(item => item.Code == code && item.Status == published)
                .OrderBy(item => item.Version, OrderByType.Desc)
                .ToListAsync(cancellationToken),
            cancellationToken);

        var latest = entities.Find(item => string.Equals(item.Code, code, StringComparison.Ordinal));
        return latest is null ? null : WorkflowDefinitionMapper.ToDefinition(latest);
    }

    /// <summary>
    /// 获取编码下的最大版本号
    /// </summary>
    /// <param name="code">流程编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最大版本号（编码不存在返回 0）</returns>
    public async Task<int> GetMaxVersionAsync(string code, CancellationToken cancellationToken = default)
    {
        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowDefinition>()
                .Where(item => item.Code == code)
                .Select(item => new { item.Code, item.Version })
                .ToListAsync(cancellationToken),
            cancellationToken);

        return entities
            .Where(item => string.Equals(item.Code, code, StringComparison.Ordinal))
            .Select(item => item.Version)
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// 查询定义列表
    /// </summary>
    /// <param name="code">流程编码（为空表示不过滤）</param>
    /// <param name="status">状态（为空表示不过滤）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义列表（按编码升序、版本降序）</returns>
    public async Task<List<WorkflowDefinition>> GetListAsync(
        string? code = null,
        WorkflowDefinitionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var statusValue = (int)(status ?? default);

        var entities = await _executor.ExecuteAsync(
            client => client.Queryable<SysWorkflowDefinition>()
                .WhereIF(code is not null, item => item.Code == code)
                .WhereIF(status is not null, item => item.Status == statusValue)
                .OrderBy(item => item.Code)
                .OrderBy(item => item.Version, OrderByType.Desc)
                .ToListAsync(cancellationToken),
            cancellationToken);

        return [.. entities
            .Where(item => code is null || string.Equals(item.Code, code, StringComparison.Ordinal))
            .Select(WorkflowDefinitionMapper.ToDefinition)];
    }

    /// <summary>
    /// 插入定义
    /// </summary>
    /// <param name="definition">定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task InsertAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var entity = WorkflowDefinitionMapper.ToEntity(definition);

        await _executor.ExecuteAsync(
            client => client.Insertable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 更新定义，标识不存在时不做任何事
    /// </summary>
    /// <param name="definition">定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var entity = WorkflowDefinitionMapper.ToEntity(definition);

        await _executor.ExecuteAsync(
            client => client.Updateable(entity).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 删除定义
    /// </summary>
    /// <param name="id">定义标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _executor.ExecuteAsync(
            client => client.Deleteable<SysWorkflowDefinition>()
                .Where(item => item.BasicId == id)
                .ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }
}
