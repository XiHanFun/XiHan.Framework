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

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.Core;

/// <summary>
/// SqlSugar 聚合根仓储实现
/// 集成领域事件处理
/// </summary>
/// <typeparam name="TAggregateRoot">聚合根类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class SqlSugarAggregateRepository<TAggregateRoot, TKey> : SqlSugarDataRepository<TAggregateRoot, TKey>, IAggregateRootRepository<TAggregateRoot, TKey>, IScopedDependency
    where TAggregateRoot : class, IAggregateRoot<TKey>, new()
    where TKey : IEquatable<TKey>
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="serviceProvider">服务提供者</param>
    public SqlSugarAggregateRepository(IUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider)
        : base(unitOfWorkManager, serviceProvider)
    {
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// 工作单元
    /// </summary>
    public IUnitOfWork UnitOfWork => _unitOfWorkManager.Current ?? throw new InvalidOperationException("当前没有活动的工作单元");

    /// <summary>
    /// 添加聚合根并处理领域事件
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加的聚合根</returns>
    public override async Task<TAggregateRoot> AddAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // 添加聚合根
        var result = await base.AddAsync(aggregate, cancellationToken);

        // 处理领域事件
        await HandleDomainEventsAsync(aggregate);

        return result;
    }

    /// <summary>
    /// 更新聚合根并处理领域事件
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新的聚合根</returns>
    public override async Task<TAggregateRoot> UpdateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // 更新聚合根
        var result = await base.UpdateAsync(aggregate, cancellationToken);

        // 处理领域事件
        await HandleDomainEventsAsync(aggregate);

        return result;
    }

    /// <summary>
    /// 删除聚合根并处理领域事件
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public override async Task DeleteAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // 删除聚合根
        await base.DeleteAsync(aggregate, cancellationToken);

        // 处理领域事件
        await HandleDomainEventsAsync(aggregate);
    }

    /// <summary>
    /// 保存聚合根变更（包括事件处理）
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public virtual async Task SaveAggregateAsync(TAggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // 如果是新实体（临时实体），则添加；否则更新
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
    /// 处理领域事件
    /// </summary>
    /// <param name="aggregate">聚合根</param>
    /// <returns>任务</returns>
    protected virtual Task HandleDomainEventsAsync(TAggregateRoot aggregate)
    {
        // 获取本地事件并添加到工作单元
        var localEvents = aggregate.GetLocalEvents();
        foreach (var eventRecord in localEvents)
        {
            UnitOfWork.AddOrReplaceLocalEvent(new UnitOfWorkEventRecord(
                eventRecord.EventData.GetType(),
                eventRecord.EventData,
                eventRecord.EventOrder));
        }

        // 获取分布式事件并添加到工作单元
        var distributedEvents = aggregate.GetDistributedEvents();
        foreach (var eventRecord in distributedEvents)
        {
            UnitOfWork.AddOrReplaceDistributedEvent(new UnitOfWorkEventRecord(
                eventRecord.EventData.GetType(),
                eventRecord.EventData,
                eventRecord.EventOrder));
        }

        // 清除聚合根中的事件（已转移到工作单元）
        aggregate.ClearLocalEvents();
        aggregate.ClearDistributedEvents();

        return Task.CompletedTask;
    }
}
