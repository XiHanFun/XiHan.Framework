// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

public sealed class UnitOfWorkMiddlewareTests
{
    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public List<string> Calls { get; } = [];

        public Result CommitResult { get; init; } = Result.Success();

        public UnitOfWorkState State { get; private set; }

        public string? TenantId { get; private set; }

        public bool IsReadOnly { get; private set; }

        public Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken)
        {
            Calls.Add("begin");
            State = UnitOfWorkState.Active;
            TenantId = options.TenantId;
            IsReadOnly = options.IsReadOnly;
            return Task.CompletedTask;
        }

        public Task<Result> CommitAsync(CancellationToken cancellationToken)
        {
            Calls.Add("commit");
            State = CommitResult.IsSuccess ? UnitOfWorkState.Committed : UnitOfWorkState.RolledBack;
            return Task.FromResult(CommitResult);
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            Calls.Add("rollback");
            State = UnitOfWorkState.RolledBack;
            return Task.CompletedTask;
        }
    }

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
        var failure = Error.Validation("PERSISTENCE_VALIDATION_999", "拒绝");

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
        var conflict = Error.Conflict(PersistenceErrors.Concurrency, "并发");
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
