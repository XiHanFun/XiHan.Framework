// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// 一次调用的事务边界。作用域生命周期；Transaction 阶段的中间件开启它，按结果提交或回滚。
/// 仓储只在活动的工作单元内工作，否则抛出携带 <see cref="PersistenceErrors.NoActiveUnitOfWork"/> 的 <see cref="FrameworkException"/>。
/// 状态机：NotStarted → Active → Committed | RolledBack；违反顺序抛出携带 <see cref="PersistenceErrors.InvalidUnitOfWorkState"/> 的 <see cref="FrameworkException"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IUnitOfWork
{
    /// <summary>
    /// 状态。
    /// </summary>
    UnitOfWorkState State { get; }

    /// <summary>
    /// 当前租户；无租户为 <see langword="null"/>。
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// 是否只读。
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// 本工作单元内写过的聚合：仓储在新增 / 更新 / 删除成功后登记，Transaction 阶段提交前把带有未派发事件的聚合交给 <see cref="IDomainEventDispatcher"/>。
    /// </summary>
    DomainEventTracker DomainEvents { get; }

    /// <summary>
    /// 开始。只能调用一次。
    /// </summary>
    Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 提交。并发冲突以 <see cref="PersistenceErrors.Concurrency"/> 返回，标识重复以 <see cref="PersistenceErrors.Duplicate"/> 返回。
    /// </summary>
    Task<Result> CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 回滚。
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
