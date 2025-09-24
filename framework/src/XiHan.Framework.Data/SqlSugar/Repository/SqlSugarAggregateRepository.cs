#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarAggregateRepository
// Guid:d4e5f6a7-b8c9-4567-8901-234567890123
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 22:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.SqlSugar.Repository;

/// <summary>
/// SqlSugar 聚合根仓储实现
/// </summary>
/// <typeparam name="TAggregateRoot">聚合根类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarAggregateRepository<TAggregateRoot, TKey> : SqlSugarRepositoryBase<TAggregateRoot, TKey>, IAggregateRootRepository<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot<TKey>, new()
    where TKey : IEquatable<TKey>
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SqlSugarAggregateRepository(ISqlSugarDbContext dbContext, IUnitOfWorkManager unitOfWorkManager)
        : base(dbContext)
    {
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// 工作单元
    /// </summary>
    protected IUnitOfWork UnitOfWork => _unitOfWorkManager.Current ?? throw new InvalidOperationException("当前没有活动的工作单元");

    /// <inheritdoc />
    public new async Task<TAggregateRoot> AddAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        var result = await base.AddAsync(aggregate, cancellationToken);
        await PublishDomainEventsAsync(aggregate);
        return result;
    }

    /// <inheritdoc />
    public new async Task<TAggregateRoot> UpdateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdateAsync(aggregate, cancellationToken);
        await PublishDomainEventsAsync(aggregate);
        return result;
    }

    /// <inheritdoc />
    public new async Task DeleteAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        await base.DeleteAsync(aggregate, cancellationToken);
        await PublishDomainEventsAsync(aggregate);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<TAggregateRoot?> GetAggregateAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return FindByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TAggregateRoot?> GetWithEventsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var aggregate = await FindByIdAsync(id, cancellationToken);
        return aggregate;
    }

    private Task PublishDomainEventsAsync(TAggregateRoot aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        foreach (var localEvent in aggregate.GetLocalEvents())
        {
            UnitOfWork.AddOrReplaceLocalEvent(new UnitOfWorkEventRecord(
                localEvent.EventData.GetType(),
                localEvent.EventData,
                localEvent.EventOrder));
        }

        foreach (var distributedEvent in aggregate.GetDistributedEvents())
        {
            UnitOfWork.AddOrReplaceDistributedEvent(new UnitOfWorkEventRecord(
                distributedEvent.EventData.GetType(),
                distributedEvent.EventData,
                distributedEvent.EventOrder));
        }

        aggregate.ClearLocalEvents();
        aggregate.ClearDistributedEvents();

        return Task.CompletedTask;
    }
}
