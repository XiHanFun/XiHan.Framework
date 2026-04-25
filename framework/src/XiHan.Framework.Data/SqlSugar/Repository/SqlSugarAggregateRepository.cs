#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarAggregateRepository
// Guid:d4e5f6a7-b8c9-4567-8901-234567890123
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 22:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 聚合根仓储实现
/// </summary>
/// <typeparam name="TAggregateRoot">聚合根类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarAggregateRepository<TAggregateRoot, TKey> : SqlSugarAuditedRepository<TAggregateRoot, TKey>, IAggregateRootRepository<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot<TKey>, new()
    where TKey : IEquatable<TKey>
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">SqlSugar 客户端解析器</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="logger">日志记录器</param>
    public SqlSugarAggregateRepository(
        ISqlSugarClientResolver clientResolver,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<SqlSugarAggregateRepository<TAggregateRoot, TKey>>? logger = null)
        : base(clientResolver)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger ?? NullLogger<SqlSugarAggregateRepository<TAggregateRoot, TKey>>.Instance;
    }

    /// <summary>
    /// 工作单元（无活动 UoW 时返回 null）
    /// </summary>
    protected IUnitOfWork? UnitOfWork => _unitOfWorkManager.Current;

    /// <summary>
    /// 添加聚合根并触发聚合根上的领域事件
    /// </summary>
    /// <param name="aggregate">聚合根实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已持久化的聚合根实例</returns>
    public override async Task<TAggregateRoot> AddAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        var result = await base.AddAsync(aggregate, cancellationToken);
        PublishDomainEvents(aggregate);
        return result;
    }

    /// <summary>
    /// 更新聚合根并触发聚合根上的领域事件
    /// </summary>
    /// <param name="aggregate">聚合根实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的聚合根实例</returns>
    public override async Task<TAggregateRoot> UpdateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdateAsync(aggregate, cancellationToken);
        PublishDomainEvents(aggregate);
        return result;
    }

    /// <summary>
    /// 删除聚合根并触发聚合根上的领域事件
    /// </summary>
    /// <param name="aggregate">聚合根实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除是否成功</returns>
    public override async Task<bool> DeleteAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        var deleted = await base.DeleteAsync(aggregate, cancellationToken);
        PublishDomainEvents(aggregate);
        return deleted;
    }

    /// <summary>
    /// 保存聚合根变更（包括事件处理）
    /// </summary>
    /// <param name="aggregate">需要持久化的聚合根实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示保存操作的任务</returns>
    public async Task SaveAggregateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate.IsTransient())
        {
            await AddAsync(aggregate, cancellationToken);
        }
        else
        {
            await UpdateAsync(aggregate, cancellationToken);
        }
    }

    /// <summary>
    /// 获取聚合根及其相关实体
    /// </summary>
    /// <param name="id">聚合根主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聚合根实例，如果不存在则返回 <c>null</c></returns>
    public Task<TAggregateRoot?> GetAggregateAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// 获取包含领域事件的聚合根
    /// </summary>
    /// <param name="id">聚合根主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含未发布领域事件的聚合根实例，如果不存在则返回 <c>null</c></returns>
    public async Task<TAggregateRoot?> GetWithEventsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var aggregate = await GetByIdAsync(id, cancellationToken);
        return aggregate;
    }

    /// <summary>
    /// 发布领域事件（无活动 UoW 时跳过并记录警告）
    /// </summary>
    /// <param name="aggregate">聚合根实例</param>
    private void PublishDomainEvents(TAggregateRoot aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var unitOfWork = UnitOfWork;
        if (unitOfWork == null)
        {
            var localCount = aggregate.GetLocalEvents().Count;
            var distributedCount = aggregate.GetDistributedEvents().Count;
            if (localCount > 0 || distributedCount > 0)
            {
                _logger.LogWarning(
                    "聚合根 {AggregateType} 有 {LocalCount} 个本地事件和 {DistributedCount} 个分布式事件待发布，但当前没有活动的工作单元，事件将被丢弃",
                    typeof(TAggregateRoot).Name, localCount, distributedCount);
            }

            aggregate.ClearLocalEvents();
            aggregate.ClearDistributedEvents();
            return;
        }

        foreach (var localEvent in aggregate.GetLocalEvents())
        {
            unitOfWork.AddOrReplaceLocalEvent(new UnitOfWorkEventRecord(
                localEvent.EventData.GetType(),
                localEvent.EventData,
                localEvent.EventOrder));
        }

        foreach (var distributedEvent in aggregate.GetDistributedEvents())
        {
            unitOfWork.AddOrReplaceDistributedEvent(new UnitOfWorkEventRecord(
                distributedEvent.EventData.GetType(),
                distributedEvent.EventData,
                distributedEvent.EventOrder));
        }

        aggregate.ClearLocalEvents();
        aggregate.ClearDistributedEvents();
    }
}
