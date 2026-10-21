// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 工作单元状态。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum UnitOfWorkState
{
    /// <summary>尚未开始。</summary>
    NotStarted = 0,

    /// <summary>进行中。</summary>
    Active = 1,

    /// <summary>已提交。</summary>
    Committed = 2,

    /// <summary>已回滚。</summary>
    RolledBack = 3,
}

/// <summary>
/// 开启工作单元的选项。
/// </summary>
/// <param name="TenantId">当前租户；多租户实体据此写入与过滤。</param>
/// <param name="IsReadOnly">只读：提交时不写入。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record UnitOfWorkOptions(string? TenantId = null, bool IsReadOnly = false)
{
    /// <summary>
    /// 无租户、可写。
    /// </summary>
    public static UnitOfWorkOptions Default { get; } = new();
}

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
