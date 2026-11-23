// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 按脚本失败并记录调用顺序的工作单元替身
/// </summary>
/// <remarks>
/// 开始、提交、回滚与释放各自可以预设抛出的异常，提交还可以预设返回的结果；用于验证管道外的外壳在每一种失败下的收尾
/// </remarks>
internal sealed class ScriptedUnitOfWork : IUnitOfWork, IAsyncDisposable
{
    /// <summary>
    /// 按发生顺序记录的调用名：begin、commit、rollback、dispose
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// <see cref="BeginAsync"/> 抛出的异常
    /// </summary>
    /// <remarks>
    /// 抛出时工作单元保持未开始
    /// </remarks>
    public Exception? BeginFailure { get; init; }

    /// <summary>
    /// <see cref="CommitAsync"/> 返回的结果
    /// </summary>
    public Result CommitResult { get; init; } = Result.Success();

    /// <summary>
    /// <see cref="CommitAsync"/> 抛出的异常
    /// </summary>
    /// <remarks>
    /// 抛出时工作单元仍活动，与关系型 Provider 提交事务抛出时一致
    /// </remarks>
    public Exception? CommitFailure { get; init; }

    /// <summary>
    /// 提交返回失败后是否仍活动
    /// </summary>
    /// <remarks>
    /// 框架的 Provider 在返回失败前已经回滚；这里可以模拟没有这样做的实现
    /// </remarks>
    public bool StaysActiveAfterFailedCommit { get; init; }

    /// <summary>
    /// <see cref="RollbackAsync"/> 抛出的异常
    /// </summary>
    /// <remarks>
    /// 抛出时工作单元仍活动，与数据库已断开时回滚事务一致
    /// </remarks>
    public Exception? RollbackFailure { get; init; }

    /// <summary>
    /// <see cref="DisposeAsync"/> 抛出的异常
    /// </summary>
    public Exception? DisposeFailure { get; init; }

    /// <summary>
    /// 开始、提交与回滚各自收到的取消令牌，按调用顺序
    /// </summary>
    public List<CancellationToken> Tokens { get; } = [];

    /// <inheritdoc />
    public UnitOfWorkState State { get; private set; }

    /// <inheritdoc />
    public UnitOfWorkOptions Options { get; private set; } = UnitOfWorkOptions.Default;

    /// <inheritdoc />
    /// <remarks>
    /// 替身不写入，从不开事务
    /// </remarks>
    public bool HasTransaction => false;

    /// <inheritdoc />
    public DomainEventTracker DomainEvents { get; } = new();

    /// <inheritdoc />
    public Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken)
    {
        Calls.Add("begin");
        Tokens.Add(cancellationToken);
        if (BeginFailure is not null)
        {
            throw BeginFailure;
        }

        State = UnitOfWorkState.Active;
        Options = options;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        Calls.Add("commit");
        Tokens.Add(cancellationToken);
        if (CommitFailure is not null)
        {
            throw CommitFailure;
        }

        if (CommitResult.IsSuccess)
        {
            State = UnitOfWorkState.Committed;
        }
        else if (!StaysActiveAfterFailedCommit)
        {
            State = UnitOfWorkState.RolledBack;
        }

        return Task.FromResult(CommitResult);
    }

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        Calls.Add("rollback");
        Tokens.Add(cancellationToken);
        if (RollbackFailure is not null)
        {
            throw RollbackFailure;
        }

        State = UnitOfWorkState.RolledBack;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Calls.Add("dispose");
        return DisposeFailure is null ? ValueTask.CompletedTask : throw DisposeFailure;
    }
}
