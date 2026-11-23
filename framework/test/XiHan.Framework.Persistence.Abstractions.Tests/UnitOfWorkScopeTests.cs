// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="UnitOfWorkScope"/> 的收尾规则
/// </summary>
/// <remarks>
/// 成功提交、失败结果回滚、提交失败原样返回、异常回滚后抛出或映射、回滚与释放自身的失败不盖掉已经确定的结果或异常、调用方取消与驱动超时的区分、装配错误不交给映射
/// </remarks>
public sealed class UnitOfWorkScopeTests : IDisposable
{
    /// <summary>
    /// 底层异常里不该外泄的文本
    /// </summary>
    private const string Secret = "Host=db.internal;Password=hunter2";

    /// <summary>
    /// 动作返回的失败
    /// </summary>
    private static readonly Error _rejected = Error.Literal("PERSISTENCE_VALIDATION_999", "Rejected.");

    /// <summary>
    /// 映射给出的错误
    /// </summary>
    private static readonly Error _unavailable = Error.Literal("PERSISTENCE_UNAVAILABLE_999", "Unavailable.");

    /// <summary>
    /// 收集外壳日志的工厂
    /// </summary>
    private readonly LogCapture _logs = new();

    [Fact]
    public async Task Success_BeginsWithTheOptions_RunsTheBodyInANewScope_AndCommits()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork);
        var options = new UnitOfWorkOptions { TenantId = "acme", IsReadOnly = true, ReadsPrimary = true };
        using var cancellation = new CancellationTokenSource();
        IServiceProvider? seen = null;

        var result = await UnitOfWorkScope.RunAsync(
            root,
            options,
            (services, ct) =>
            {
                seen = services;
                // 动作执行时工作单元已经开始，令牌是调用方给的那个
                Assert.Equal(UnitOfWorkState.Active, unitOfWork.State);
                Assert.Equal(cancellation.Token, ct);
                Assert.Same(unitOfWork, services.GetRequiredService<IUnitOfWork>());
                return ValueTask.FromResult(Result.Success(42));
            },
            Logger(),
            cancellation.Token);

        Assert.Equal(42, result.ValueOrThrow());
        Assert.Same(options, unitOfWork.Options);
        Assert.NotNull(seen);
        Assert.NotSame(root, seen);
        Assert.Equal(["begin", "commit", "dispose"], unitOfWork.Calls);
        Assert.All(unitOfWork.Tokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public async Task FailedBody_RollsBack_AndReturnsTheFailureAsIs()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork);

        var result = await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Failure<int>(_rejected)), Logger(), CancellationToken.None);

        Assert.Same(_rejected, result.Error);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task FailedCommit_IsReturnedAsIs_WithoutASecondRollback()
    {
        var conflict = Error.Literal(PersistenceErrors.Concurrency, "Concurrency conflict.");
        var unitOfWork = new ScriptedUnitOfWork { CommitResult = conflict };
        await using var root = Root(unitOfWork);

        // 带映射的重载也原样返回：提交返回的失败不是抛出的异常
        var result = await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Success(1)), _ => throw new InvalidOperationException("The fault mapper must not run."), Logger(), CancellationToken.None);

        Assert.Same(conflict, result.Error);
        Assert.Equal(["begin", "commit", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task FailedCommit_ThatLeavesTheUnitOfWorkActive_IsRolledBack()
    {
        var conflict = Error.Literal(PersistenceErrors.Concurrency, "Concurrency conflict.");
        var unitOfWork = new ScriptedUnitOfWork { CommitResult = conflict, StaysActiveAfterFailedCommit = true };
        await using var root = Root(unitOfWork);

        var result = await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Success(1)), Logger(), CancellationToken.None);

        Assert.Same(conflict, result.Error);
        Assert.Equal(["begin", "commit", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task ThrowingBody_RollsBack_AndRethrowsTheSameException()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork);
        var failure = new InvalidOperationException(Secret);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await UnitOfWorkScope.RunAsync<int>(root, UnitOfWorkOptions.Default, (_, _) => throw failure, Logger(), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task ThrowingBody_WithAFaultMapper_RollsBack_AndReturnsTheMappedError()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork);
        var failure = new InvalidOperationException(Secret);
        Exception? handed = null;

        var result = await UnitOfWorkScope.RunAsync<int>(
            root,
            UnitOfWorkOptions.Default,
            (_, _) => throw failure,
            exception =>
            {
                handed = exception;
                // 映射执行时已经回滚完
                Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
                return _unavailable;
            },
            Logger(),
            CancellationToken.None);

        Assert.Same(failure, handed);
        Assert.Same(_unavailable, result.Error);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        // 外壳不替调用方记故障本身
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public async Task ThrowingBegin_IsNotRolledBack()
    {
        var failure = new TimeoutException(Secret);
        var unitOfWork = new ScriptedUnitOfWork { BeginFailure = failure };
        await using var root = Root(unitOfWork);

        var mapped = await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Success(1)), _ => _unavailable, Logger(), CancellationToken.None);
        Assert.Same(_unavailable, mapped.Error);
        // 工作单元没开起来：不回滚，也不执行动作
        Assert.Equal(["begin", "dispose"], unitOfWork.Calls);

        var thrown = await Assert.ThrowsAsync<TimeoutException>(async () => await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Success(1)), Logger(), CancellationToken.None));
        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task ThrowingCommit_RollsBack_AndIsHandedToTheFaultMapper()
    {
        var failure = new InvalidOperationException(Secret);
        var unitOfWork = new ScriptedUnitOfWork { CommitFailure = failure };
        await using var root = Root(unitOfWork);
        Exception? handed = null;

        var result = await UnitOfWorkScope.RunAsync(
            root,
            UnitOfWorkOptions.Default,
            static (_, _) => ValueTask.FromResult(Result.Success(1)),
            exception =>
            {
                handed = exception;
                return _unavailable;
            },
            Logger(),
            CancellationToken.None);

        Assert.Same(failure, handed);
        Assert.Same(_unavailable, result.Error);
        Assert.Equal(["begin", "commit", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task ThrowingRollback_DoesNotReplaceAFailedResult()
    {
        var rollback = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback };
        await using var root = Root(unitOfWork);

        var result = await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Failure<int>(_rejected)), Logger(), CancellationToken.None);

        // 动作的失败原样返回，回滚的异常只进日志
        Assert.Same(_rejected, result.Error);
        var entry = Assert.Single(_logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(rollback, entry.Exception);
        Assert.Contains("Rolling back", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThrowingRollback_DoesNotReplaceTheOriginalException()
    {
        var rollback = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback };
        await using var root = Root(unitOfWork);
        var failure = new TimeoutException(Secret);

        var thrown = await Assert.ThrowsAsync<TimeoutException>(async () => await UnitOfWorkScope.RunAsync<int>(root, UnitOfWorkOptions.Default, (_, _) => throw failure, Logger(), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Same(rollback, Assert.Single(_logs.Entries).Exception);
    }

    [Fact]
    public async Task ThrowingRollback_DoesNotReplaceTheMappedError()
    {
        var rollback = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback };
        await using var root = Root(unitOfWork);
        var failure = new TimeoutException(Secret);
        Exception? handed = null;

        var result = await UnitOfWorkScope.RunAsync<int>(
            root,
            UnitOfWorkOptions.Default,
            (_, _) => throw failure,
            exception =>
            {
                handed = exception;
                return _unavailable;
            },
            Logger(),
            CancellationToken.None);

        // 交给映射的是原异常，不是回滚的异常
        Assert.Same(failure, handed);
        Assert.Same(_unavailable, result.Error);
        Assert.Same(rollback, Assert.Single(_logs.Entries).Exception);
    }

    [Fact]
    public async Task ThrowingDispose_AfterAFailedRollback_DoesNotReplaceTheOriginalException()
    {
        // 数据库断开时的真实形状：回滚抛出，Provider 在释放作用域时再回滚一次、再抛一次
        var rollback = new InvalidOperationException("The connection is closed.");
        var dispose = new InvalidOperationException("The connection is still closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback, DisposeFailure = dispose };
        await using var root = Root(unitOfWork);
        var failure = new TimeoutException(Secret);

        var thrown = await Assert.ThrowsAsync<TimeoutException>(async () => await UnitOfWorkScope.RunAsync<int>(root, UnitOfWorkOptions.Default, (_, _) => throw failure, Logger(), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Equal([rollback, dispose], _logs.Entries.Select(static entry => entry.Exception));
        Assert.All(_logs.Entries, static entry => Assert.Equal(LogLevel.Warning, entry.Level));
    }

    [Fact]
    public async Task ThrowingDispose_DoesNotReplaceADecidedResult()
    {
        var dispose = new InvalidOperationException("The connection is closed.");

        // 已提交：释放失败不把成功改成异常
        var committed = new ScriptedUnitOfWork { DisposeFailure = dispose };
        await using (var root = Root(committed))
        {
            var result = await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Success(7)), Logger(), CancellationToken.None);
            Assert.Equal(7, result.ValueOrThrow());
        }

        // 已映射：释放失败不盖掉映射出的错误
        var faulted = new ScriptedUnitOfWork { DisposeFailure = dispose };
        await using (var root = Root(faulted))
        {
            var result = await UnitOfWorkScope.RunAsync<int>(root, UnitOfWorkOptions.Default, static (_, _) => throw new TimeoutException(Secret), _ => _unavailable, Logger(), CancellationToken.None);
            Assert.Same(_unavailable, result.Error);
        }

        Assert.Equal(2, _logs.Entries.Count);
        Assert.All(_logs.Entries, entry =>
        {
            Assert.Same(dispose, entry.Exception);
            Assert.Contains("Disposing the service scope", entry.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CallerCancellation_RollsBackWithoutTheCallersToken_AndIsNeverMapped()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork);
        using var cancellation = new CancellationTokenSource();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await UnitOfWorkScope.RunAsync<int>(
            root,
            UnitOfWorkOptions.Default,
            async (_, ct) =>
            {
                await cancellation.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return 1;
            },
            _ => throw new InvalidOperationException("The fault mapper must not run on caller cancellation."),
            Logger(),
            cancellation.Token));

        Assert.Equal(cancellation.Token, thrown.CancellationToken);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        // 回滚不带调用方已经取消的令牌，否则回滚自己也会被取消
        Assert.Equal(CancellationToken.None, unitOfWork.Tokens[^1]);
        Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
    }

    [Fact]
    public async Task CallerCancellation_WhoseRollbackThrows_StillSurfacesAsCancellation()
    {
        var rollback = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback };
        await using var root = Root(unitOfWork);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await UnitOfWorkScope.RunAsync<int>(
            root,
            UnitOfWorkOptions.Default,
            static (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return ValueTask.FromResult(Result.Success(1));
            },
            Logger(),
            cancellation.Token));

        Assert.Same(rollback, Assert.Single(_logs.Entries).Exception);
    }

    [Fact]
    public async Task CancellationException_WithoutACancelledCallerToken_IsAFault()
    {
        // 驱动把命令超时报告成取消：调用方的令牌没有取消，这是存储故障，不是取消
        var timeout = new TaskCanceledException(Secret);

        var mappedUnitOfWork = new ScriptedUnitOfWork();
        await using (var root = Root(mappedUnitOfWork))
        {
            Exception? handed = null;
            var result = await UnitOfWorkScope.RunAsync<int>(
                root,
                UnitOfWorkOptions.Default,
                (_, _) => throw timeout,
                exception =>
                {
                    handed = exception;
                    return _unavailable;
                },
                Logger(),
                CancellationToken.None);

            Assert.Same(timeout, handed);
            Assert.Same(_unavailable, result.Error);
            Assert.Equal(["begin", "rollback", "dispose"], mappedUnitOfWork.Calls);
        }

        // 不带映射的重载没有可归的错误，原样抛出
        var thrownUnitOfWork = new ScriptedUnitOfWork();
        await using (var root = Root(thrownUnitOfWork))
        {
            var thrown = await Assert.ThrowsAsync<TaskCanceledException>(async () => await UnitOfWorkScope.RunAsync<int>(root, UnitOfWorkOptions.Default, (_, _) => throw timeout, Logger(), CancellationToken.None));
            Assert.Same(timeout, thrown);
            Assert.Equal(["begin", "rollback", "dispose"], thrownUnitOfWork.Calls);
        }
    }

    [Fact]
    public async Task OtherException_WhileTheCallerTokenIsCancelled_IsStillAFault()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // 只有取消异常才按取消抛出：令牌取消时抛出的别的异常仍交给映射
        var result = await UnitOfWorkScope.RunAsync<int>(root, UnitOfWorkOptions.Default, static (_, _) => throw new InvalidOperationException(Secret), _ => _unavailable, Logger(), cancellation.Token);

        Assert.Same(_unavailable, result.Error);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task MissingUnitOfWork_IsAnAssemblyError_AndIsNeverMapped()
    {
        await using var root = new ServiceCollection().BuildServiceProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await UnitOfWorkScope.RunAsync(
            root,
            UnitOfWorkOptions.Default,
            static (_, _) => ValueTask.FromResult(Result.Success(1)),
            _ => throw new InvalidOperationException("The fault mapper must not run for an assembly error."),
            Logger(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Prepare_RunsOnTheNewScope_BeforeTheUnitOfWorkIsResolved()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        var order = new List<string>();
        await using var root = new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ =>
            {
                order.Add("resolve");
                return unitOfWork;
            })
            .BuildServiceProvider();
        IServiceProvider? prepared = null;
        IServiceProvider? seen = null;

        var result = await UnitOfWorkScope.RunAsync(
            root,
            services =>
            {
                order.Add("prepare");
                prepared = services;
            },
            UnitOfWorkOptions.Default,
            (services, _) =>
            {
                order.Add("body");
                seen = services;
                return ValueTask.FromResult(Result.Success(7));
            },
            Logger(),
            CancellationToken.None);

        Assert.Equal(7, result.ValueOrThrow());
        Assert.Equal(["prepare", "resolve", "body"], order);
        // 准备与动作拿到的是同一个新作用域
        Assert.NotSame(root, prepared);
        Assert.Same(prepared, seen);
        Assert.Equal(["begin", "commit", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task ThrowingPrepare_IsAnAssemblyError_AndTheUnitOfWorkIsNeverResolved()
    {
        var resolved = false;
        await using var root = new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ =>
            {
                resolved = true;
                return new ScriptedUnitOfWork();
            })
            .BuildServiceProvider();
        var failure = new InvalidOperationException("The scope is already bound to a shard.");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await UnitOfWorkScope.RunAsync(root, _ => throw failure, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Success(1)), Logger(), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.False(resolved);
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public async Task PreparedRun_FollowsTheSameClosingRules()
    {
        // 回滚与释放都抛出：带准备的重载走的是同一份收尾，抛出的仍是原异常
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed."), DisposeFailure = new InvalidOperationException("The connection is closed.") };
        await using var root = Root(unitOfWork);
        var failure = new TimeoutException(Secret);

        var thrown = await Assert.ThrowsAsync<TimeoutException>(async () => await UnitOfWorkScope.RunAsync<int>(root, static _ => { }, UnitOfWorkOptions.Default, (_, _) => throw failure, Logger(), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Equal(2, _logs.Entries.Count(static e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task OverloadsWithoutAValue_FollowTheSameRules()
    {
        var committed = new ScriptedUnitOfWork();
        await using (var root = Root(committed))
        {
            Assert.True((await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Success()), Logger(), CancellationToken.None)).IsSuccess);
            Assert.Equal(["begin", "commit", "dispose"], committed.Calls);
        }

        var rejected = new ScriptedUnitOfWork();
        await using (var root = Root(rejected))
        {
            var result = await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, static (_, _) => ValueTask.FromResult(Result.Failure(_rejected)), Logger(), CancellationToken.None);
            Assert.Same(_rejected, result.Error);
            Assert.Equal(["begin", "rollback", "dispose"], rejected.Calls);
        }

        var faulted = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed.") };
        await using (var root = Root(faulted))
        {
            var failure = new TimeoutException(Secret);
            var mapped = await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, (_, _) => throw failure, _ => _unavailable, Logger(), CancellationToken.None);
            Assert.Same(_unavailable, mapped.Error);
        }

        var thrown = new ScriptedUnitOfWork();
        await using (var root = Root(thrown))
        {
            var failure = new TimeoutException(Secret);
            Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(async () => await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, (_, _) => throw failure, Logger(), CancellationToken.None)));
            Assert.Equal(["begin", "rollback", "dispose"], thrown.Calls);
        }
    }

    [Fact]
    public async Task NullArguments_AreRejectedBeforeAScopeIsOpened()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork);
        var logger = Logger();
        Func<IServiceProvider, CancellationToken, ValueTask<Result<int>>> body = static (_, _) => ValueTask.FromResult(Result.Success(1));
        Func<IServiceProvider, CancellationToken, ValueTask<Result>> plain = static (_, _) => ValueTask.FromResult(Result.Success());
        Func<Exception, Error> fault = static _ => _unavailable;

        await RejectsAsync("root", async () => await UnitOfWorkScope.RunAsync(null!, UnitOfWorkOptions.Default, body, logger, CancellationToken.None));
        await RejectsAsync("options", async () => await UnitOfWorkScope.RunAsync(root, null!, body, logger, CancellationToken.None));
        await RejectsAsync("body", async () => await UnitOfWorkScope.RunAsync<int>(root, UnitOfWorkOptions.Default, null!, logger, CancellationToken.None));
        await RejectsAsync("logger", async () => await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, body, null!, CancellationToken.None));
        await RejectsAsync("fault", async () => await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, body, null!, logger, CancellationToken.None));
        await RejectsAsync("prepare", async () => await UnitOfWorkScope.RunAsync(root, (Action<IServiceProvider>)null!, UnitOfWorkOptions.Default, body, logger, CancellationToken.None));
        await RejectsAsync("body", async () => await UnitOfWorkScope.RunAsync<int>(root, static _ => { }, UnitOfWorkOptions.Default, null!, logger, CancellationToken.None));
        await RejectsAsync("root", async () => await UnitOfWorkScope.RunAsync(null!, UnitOfWorkOptions.Default, plain, logger, CancellationToken.None));
        await RejectsAsync("fault", async () => await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, plain, null!, logger, CancellationToken.None));
        await RejectsAsync("logger", async () => await UnitOfWorkScope.RunAsync(root, UnitOfWorkOptions.Default, plain, fault, null!, CancellationToken.None));

        // 参数在开作用域之前校验：工作单元一次也没被碰过
        Assert.Empty(unitOfWork.Calls);
    }

    /// <inheritdoc />
    public void Dispose() => _logs.Dispose();

    /// <summary>
    /// 调用以这个参数名拒绝空参数
    /// </summary>
    private static Task<ArgumentNullException> RejectsAsync(string parameter, Func<ValueTask> call)
        => Assert.ThrowsAsync<ArgumentNullException>(parameter, async () => await call());

    /// <summary>
    /// 只装了这个工作单元替身的容器
    /// </summary>
    private static ServiceProvider Root(ScriptedUnitOfWork unitOfWork)
        => new ServiceCollection().AddScoped<IUnitOfWork>(_ => unitOfWork).BuildServiceProvider();

    /// <summary>
    /// 交给外壳的日志
    /// </summary>
    private ILogger Logger() => _logs.CreateLogger(nameof(UnitOfWorkScopeTests));
}
