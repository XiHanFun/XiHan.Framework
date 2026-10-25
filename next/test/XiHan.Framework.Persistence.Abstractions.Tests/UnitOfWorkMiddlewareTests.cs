// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="UnitOfWorkMiddleware"/> 的提交与回滚决策：成功提交、失败结果回滚、异常回滚并重抛、提交失败覆盖结果、租户透传。
/// </summary>
public sealed class UnitOfWorkMiddlewareTests
{
    /// <summary>
    /// 记录调用顺序的工作单元替身；提交结果可预设，用于验证中间件对各种结果的处理。
    /// </summary>
    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        /// <summary>
        /// 按发生顺序记录的调用名：begin、commit、rollback。
        /// </summary>
        public List<string> Calls { get; } = [];

        /// <summary>
        /// <see cref="CommitAsync"/> 返回的预设结果。
        /// </summary>
        public Result CommitResult { get; init; } = Result.Success();

        /// <inheritdoc />
        public UnitOfWorkState State { get; private set; }

        /// <inheritdoc />
        public string? TenantId { get; private set; }

        /// <inheritdoc />
        public bool IsReadOnly { get; private set; }

        /// <inheritdoc />
        public Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken)
        {
            Calls.Add("begin");
            State = UnitOfWorkState.Active;
            TenantId = options.TenantId;
            IsReadOnly = options.IsReadOnly;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<Result> CommitAsync(CancellationToken cancellationToken)
        {
            Calls.Add("commit");
            State = CommitResult.IsSuccess ? UnitOfWorkState.Committed : UnitOfWorkState.RolledBack;
            return Task.FromResult(CommitResult);
        }

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            Calls.Add("rollback");
            State = UnitOfWorkState.RolledBack;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 构造只注册了替身工作单元的调用上下文。
    /// </summary>
    private static (InvocationContext Context, RecordingUnitOfWork UnitOfWork) Arrange(Result? commitResult = null)
    {
        var unitOfWork = new RecordingUnitOfWork { CommitResult = commitResult ?? Result.Success() };
        var services = new ServiceCollection().AddSingleton<IUnitOfWork>(unitOfWork).BuildServiceProvider();
        return (TestInvocationContext.Create(services), unitOfWork);
    }

    [Fact]
    public async Task SuccessfulInvocation_BeginsThenCommits()
    {
        var (context, unitOfWork) = Arrange();

        await new UnitOfWorkMiddleware().InvokeAsync(context, static _ => ValueTask.CompletedTask);

        Assert.Equal(["begin", "commit"], unitOfWork.Calls);
        Assert.Equal(UnitOfWorkState.Committed, unitOfWork.State);
        Assert.False(context.GetOutcome().HasValue);
    }

    [Fact]
    public async Task FailureOutcome_RollsBackWithoutCommit_AndKeepsOutcome()
    {
        var (context, unitOfWork) = Arrange();
        var failure = Error.Literal("PERSISTENCE_VALIDATION_999", "Rejected.");

        await new UnitOfWorkMiddleware().InvokeAsync(context, ctx =>
        {
            ctx.SetOutcome(failure);
            return ValueTask.CompletedTask;
        });

        Assert.Equal(["begin", "rollback"], unitOfWork.Calls);
        Assert.Equal(failure, context.GetOutcome().ValueOrThrow().Error);
    }

    [Fact]
    public async Task Exception_RollsBack_AndRethrows()
    {
        var (context, unitOfWork) = Arrange();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new UnitOfWorkMiddleware().InvokeAsync(context, static _ => throw new InvalidOperationException("boom")));

        Assert.Equal(["begin", "rollback"], unitOfWork.Calls);
    }

    [Fact]
    public async Task CommitFailure_BecomesOutcome()
    {
        var conflict = Error.Literal(PersistenceErrors.Concurrency, "Concurrency conflict.");
        var (context, unitOfWork) = Arrange(commitResult: conflict);

        await new UnitOfWorkMiddleware().InvokeAsync(context, static _ => ValueTask.CompletedTask);

        Assert.Equal(["begin", "commit"], unitOfWork.Calls);
        Assert.Equal(conflict, context.GetOutcome().ValueOrThrow().Error);
    }

    [Fact]
    public async Task TenantFromSecurityContext_IsPassedToBegin()
    {
        var (context, unitOfWork) = Arrange();
        context.SetTenant(new TenantContext("acme"));

        await new UnitOfWorkMiddleware().InvokeAsync(context, static _ => ValueTask.CompletedTask);

        Assert.Equal("acme", unitOfWork.TenantId);
        Assert.False(unitOfWork.IsReadOnly);
    }
}
