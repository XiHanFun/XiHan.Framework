// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// 一次调用的事务边界
/// </summary>
/// <remarks>
/// 作用域生命周期；Transaction 阶段的中间件开启它，按结果提交或回滚。仓储只在活动的工作单元内工作，否则抛出携带 <see cref="PersistenceErrors.NoActiveUnitOfWork"/> 的 <see cref="FrameworkException"/>。状态机：NotStarted → Active → Committed | RolledBack；违反顺序抛出携带 <see cref="PersistenceErrors.InvalidUnitOfWorkState"/> 的 <see cref="FrameworkException"/>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IUnitOfWork
{
    /// <summary>
    /// 状态
    /// </summary>
    UnitOfWorkState State { get; }

    /// <summary>
    /// 当前租户
    /// </summary>
    /// <remarks>
    /// 无租户为 <see langword="null"/>
    /// </remarks>
    string? TenantId { get; }

    /// <summary>
    /// 是否只读
    /// </summary>
    /// <remarks>
    /// 只读工作单元不开数据库事务，写入以 <see cref="PersistenceErrors.ReadOnly"/> 拒绝；拒绝过写入的工作单元提交时回滚并返回同一错误
    /// </remarks>
    bool IsReadOnly { get; }

    /// <summary>
    /// 是否已开数据库事务
    /// </summary>
    /// <remarks>
    /// 事务延迟开启，<see cref="State"/> 为 <see cref="UnitOfWorkState.Active"/> 说明不了事务开没开。可写工作单元里第一次写入时开启：仓储的单条写入或集合非空的批量写入一经调用即开启（不论这次写入的结果），Provider 的直接写入入口（<c>EnsureTransactionAsync</c>）同样开启；查找、查询、计数、空批量不开启。开启后为真，直到提交（成功或返回失败）或回滚；未开始、已提交、已回滚都为假，只读工作单元恒为假。多连接时任一连接开着事务即为真；绑定命名连接的工作单元只报告那个连接。内存实现没有数据库事务，第一次写入调用起同样为真（覆盖层从此可能有未提交、随回滚丢弃的变更）。调用里要立即对外产生、不随回滚撤回的副作用（立即推送、调用外部系统）之前据此判断：为真时本工作单元已有可能随回滚消失的写入（RFC-0005「是否已开数据库事务」）
    /// </remarks>
    bool HasTransaction { get; }

    /// <summary>
    /// 本工作单元内写过的聚合
    /// </summary>
    /// <remarks>
    /// 仓储在新增 / 更新 / 删除成功后登记，Transaction 阶段提交前把带有未派发事件的聚合交给 <see cref="IDomainEventDispatcher"/>
    /// </remarks>
    DomainEventTracker DomainEvents { get; }

    /// <summary>
    /// 开始
    /// </summary>
    /// <remarks>
    /// 只能调用一次
    /// </remarks>
    Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 提交
    /// </summary>
    /// <remarks>
    /// 并发冲突以 <see cref="PersistenceErrors.Concurrency"/> 返回，标识重复以 <see cref="PersistenceErrors.Duplicate"/> 返回，都不抛异常。两个工作单元先后新增同一标识时后到者失败：内存实现在提交时发现；关系型 Provider 在写入时由主键约束发现，仓储返回标识重复后本工作单元的提交只回滚并返回同一错误
    /// </remarks>
    Task<Result> CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 回滚
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
