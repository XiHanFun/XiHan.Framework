// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="UnitOfWorkMiddleware"/> 的提交与回滚决策
/// </summary>
/// <remarks>
/// 成功提交、失败结果回滚、异常回滚并重抛、提交失败覆盖结果、租户透传、按契约开只读工作单元、只读写入与取出条数超限的异常转为结果、同一作用域的嵌套调用
/// </remarks>
public sealed class UnitOfWorkMiddlewareTests
{
    /// <summary>
    /// 记录调用顺序的工作单元替身
    /// </summary>
    /// <remarks>
    /// 提交结果可预设，用于验证中间件对各种结果的处理
    /// </remarks>
    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        /// <summary>
        /// 按发生顺序记录的调用名：begin、commit、rollback
        /// </summary>
        public List<string> Calls { get; } = [];

        /// <summary>
        /// <see cref="CommitAsync"/> 返回的预设结果
        /// </summary>
        public Result CommitResult { get; init; } = Result.Success();

        /// <inheritdoc />
        public UnitOfWorkState State { get; private set; }

        /// <inheritdoc />
        public string? TenantId { get; private set; }

        /// <inheritdoc />
        public bool IsReadOnly { get; private set; }

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
    /// 构造只注册了替身工作单元的调用上下文
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

    [Theory]
    [InlineData("orders.get", false, true)]
    [InlineData("orders.list", false, true)]
    [InlineData("departments.tree", true, true)]
    [InlineData("departments.tree", false, false)]
    [InlineData("orders.create", false, false)]
    public async Task OperationContract_DecidesWhetherTheUnitOfWorkIsReadOnly(string operationId, bool declaredReadOnly, bool expected)
    {
        var (context, unitOfWork) = Arrange();
        context.SetOperation(new ContractOnlyDescriptor(new OperationContract(operationId) { IsReadOnly = declaredReadOnly }));

        await new UnitOfWorkMiddleware().InvokeAsync(context, static _ => ValueTask.CompletedTask);

        Assert.Equal(expected, unitOfWork.IsReadOnly);
        Assert.Equal(["begin", "commit"], unitOfWork.Calls);
    }

    [Fact]
    public async Task ReadOnlyDirectWrite_RollsBack_AndBecomesUnexpectedOutcome()
    {
        var (context, unitOfWork) = Arrange();
        context.SetOperation(new ContractOnlyDescriptor(new OperationContract("orders.get")));

        // Provider 的直接写入入口在只读工作单元里抛出：是操作实现的缺陷（500 类），转为同码结果而不是笼统的 KERNEL_UNEXPECTED_001
        await new UnitOfWorkMiddleware().InvokeAsync(context, static _ => throw new FrameworkException(PersistenceErrors.ReadOnly, "The unit of work is read-only and does not accept writes."));

        Assert.Equal(["begin", "rollback"], unitOfWork.Calls);
        var error = context.GetOutcome().ValueOrThrow().Error!;
        Assert.Equal(PersistenceErrors.ReadOnly, error.Code);
        Assert.Equal("PERSISTENCE_UNEXPECTED_001", error.Code);
        Assert.Equal("UNEXPECTED", error.Kind);
        Assert.Equal(PersistenceMessages.ReadOnly, error.MessageKey);
    }

    [Fact]
    public async Task TakeAboveTheMaximum_RollsBack_AndBecomesValidationOutcome()
    {
        var (context, unitOfWork) = Arrange();

        // 处理器用调用方给的页大小构造查询：超过上限在构造时抛出，Transaction 阶段转为带请求条数与上限的校验结果
        await new UnitOfWorkMiddleware().InvokeAsync(context, static call =>
        {
            call.Items["query"] = EntityQuery.All<Widget>().Page(0, 5000);
            return ValueTask.CompletedTask;
        });

        Assert.Equal(["begin", "rollback"], unitOfWork.Calls);
        var error = context.GetOutcome().ValueOrThrow().Error!;
        Assert.Equal(PersistenceErrors.TakeExceeded, error.Code);
        Assert.Equal("VALIDATION", error.Kind);
        Assert.Equal(PersistenceMessages.TakeExceeded, error.MessageKey);
        Assert.Equal(5000, error.Details!["take"]);
        Assert.Equal(EntityQuery.DefaultMaxTake, error.Details["max"]);
    }

    [Fact]
    public async Task OtherFrameworkExceptions_RollBack_AndRethrow()
    {
        var (context, unitOfWork) = Arrange();

        var thrown = await Assert.ThrowsAsync<FrameworkException>(async () =>
            await new UnitOfWorkMiddleware().InvokeAsync(context, static _ => throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, "No unit of work.")));

        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, thrown.Code);
        Assert.Equal(["begin", "rollback"], unitOfWork.Calls);
    }

    [Fact]
    public async Task UnitOfWorkAlreadyStarted_EndsTheNestedInvocation_WithoutRunningTheHandler()
    {
        var (context, unitOfWork) = Arrange();
        // 外层调用已经开始了作用域里的工作单元
        await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        var ran = false;

        await new UnitOfWorkMiddleware().InvokeAsync(context, _ =>
        {
            ran = true;
            return ValueTask.CompletedTask;
        });

        Assert.False(ran);
        Assert.Equal(["begin"], unitOfWork.Calls);
        Assert.Equal(UnitOfWorkState.Active, unitOfWork.State);
        var error = context.GetOutcome().ValueOrThrow().Error!;
        Assert.Equal(PersistenceErrors.InvalidUnitOfWorkState, error.Code);
        Assert.Equal(PersistenceMessages.NestedInvocation, error.MessageKey);
    }
}
