// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Database.Entities;
using XiHan.Framework.Scheduling.Database.Leadership;
using XiHan.Framework.Scheduling.Database.Storage;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 租约表故障时释放、探针与管道执行的收尾与归类
/// </summary>
/// <remarks>
/// 回滚与释放作用域再失败也不盖掉已经确定的结果或原异常；存储不可达的原因只写异常类型全名（框架异常写它的错误码），异常消息不进错误；调用方取消回滚后抛出，驱动以取消异常报告的超时是存储不可达（ADR-0259）
/// </remarks>
public sealed class LeaseFaultTests : IAsyncLifetime
{
    /// <summary>
    /// 底层异常里不该外泄的文本
    /// </summary>
    private const string Secret = "Host=leases.internal;Password=hunter2";

    /// <summary>
    /// 租约资源名
    /// </summary>
    private const string Resource = "lease.fault";

    /// <summary>
    /// 本节点的令牌
    /// </summary>
    private const string Token = "token-a";

    /// <summary>
    /// 提供应用实例的数据库与宿主
    /// </summary>
    /// <remarks>
    /// 被测对象读写的是替身容器，宿主只用来取一个在服务中的应用
    /// </remarks>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 被测对象写入的日志
    /// </summary>
    private readonly RecordingLogger _log = new();

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Release_WhenTheRowIsGone_Succeeds_EvenIfClosingTheUnitOfWorkThrows()
    {
        var host = await _database.StartHostAsync();
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed."), DisposeFailure = new InvalidOperationException("The connection is still closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string>());

        var released = await Rows(root, host.App).ReleaseAsync(Resource, Token, CancellationToken.None);

        // 行没了就是已释放：收尾再失败也不把这个成功改成异常
        Assert.True(released.IsSuccess, released.Error?.Code);
        Assert.NotEqual(UnitOfWorkState.Active, unitOfWork.State);
    }

    [Fact]
    public async Task Release_WhoseRemoveFails_ReturnsThatFailure_EvenIfTheRollbackThrows()
    {
        var host = await _database.StartHostAsync();
        var full = Error.Literal(PersistenceErrors.CapacityExceeded, "The store is full.");
        var rollback = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback };
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string> { Found = Row(), WriteResult = full });

        var released = await Rows(root, host.App).ReleaseAsync(Resource, Token, CancellationToken.None);

        Assert.Same(full, released.Error);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, rollback));
    }

    [Fact]
    public async Task Release_WhoseRemoveRacesWithAnotherNode_IsReleased()
    {
        var host = await _database.StartHostAsync();
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string> { Found = Row(), WriteResult = Error.Literal(PersistenceErrors.Concurrency, "Concurrency conflict.") });

        Assert.True((await Rows(root, host.App).ReleaseAsync(Resource, Token, CancellationToken.None)).IsSuccess);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);

        // 提交时才撞上别的节点同样视为已释放
        var committing = new ScriptedUnitOfWork { CommitResult = Error.Literal(PersistenceErrors.Concurrency, "Concurrency conflict.") };
        await using var second = Root(committing, new ScriptedRepository<SchedulerLeaseEntity, string> { Found = Row() });
        Assert.True((await Rows(second, host.App).ReleaseAsync(Resource, Token, CancellationToken.None)).IsSuccess);
        Assert.Equal(["begin", "commit", "dispose"], committing.Calls);
    }

    [Fact]
    public async Task Release_ThrowingStore_IsStoreUnavailable_WithTheExceptionTypeOnly()
    {
        var host = await _database.StartHostAsync();
        var failure = new TimeoutException(Secret);
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string> { Failure = failure });

        var released = await Rows(root, host.App).ReleaseAsync(Resource, Token, CancellationToken.None);

        Assert.Equal(SchedulingErrors.StoreUnavailable, released.Error?.Code);
        Assert.Equal(typeof(TimeoutException).FullName, released.Error!.Details!["reason"]);
        Assert.DoesNotContain(Secret, Flatten(released.Error), StringComparison.Ordinal);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        // 完整异常只进日志
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, failure));
    }

    [Fact]
    public async Task Release_ThrowingStore_IsStillStoreUnavailable_WhenRollbackAndDisposeThrow()
    {
        // 数据库断开：删行抛出，回滚再抛出，Provider 释放作用域时再回滚一次、再抛一次
        var host = await _database.StartHostAsync();
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed."), DisposeFailure = new InvalidOperationException("The connection is still closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string> { Failure = new TimeoutException(Secret) });

        var released = await Rows(root, host.App).ReleaseAsync(Resource, Token, CancellationToken.None);

        Assert.Equal(SchedulingErrors.StoreUnavailable, released.Error?.Code);
        Assert.Equal(typeof(TimeoutException).FullName, released.Error!.Details!["reason"]);
    }

    [Fact]
    public async Task Release_FrameworkException_IsStoreUnavailable_WithItsCodeAsTheReason()
    {
        var host = await _database.StartHostAsync();
        await using var root = Root(new ScriptedUnitOfWork(), new ScriptedRepository<SchedulerLeaseEntity, string> { Failure = new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, "There is no active unit of work.") });

        var released = await Rows(root, host.App).ReleaseAsync(Resource, Token, CancellationToken.None);

        Assert.Equal(SchedulingErrors.StoreUnavailable, released.Error?.Code);
        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, released.Error!.Details!["reason"]);
        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, released.Error.Inner?.Code);
    }

    [Fact]
    public async Task Release_CallerCancellation_RollsBack_AndThrows()
    {
        var host = await _database.StartHostAsync();
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string> { Found = Row() });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Rows(root, host.App).ReleaseAsync(Resource, Token, cancellation.Token));

        Assert.Equal(cancellation.Token, thrown.CancellationToken);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Equal(CancellationToken.None, unitOfWork.Tokens[^1]);
    }

    [Fact]
    public async Task Release_TimeoutReportedAsCancellation_IsStoreUnavailable()
    {
        // 驱动把命令超时报告成取消异常，调用方的令牌并没有取消：行没删掉，如实报存储不可达
        var host = await _database.StartHostAsync();
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string> { Failure = new TaskCanceledException(Secret) });

        var released = await Rows(root, host.App).ReleaseAsync(Resource, Token, CancellationToken.None);

        Assert.Equal(SchedulingErrors.StoreUnavailable, released.Error?.Code);
        Assert.Equal(typeof(TaskCanceledException).FullName, released.Error!.Details!["reason"]);
        Assert.DoesNotContain(Secret, Flatten(released.Error), StringComparison.Ordinal);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Probe_ReadsTheRowInAReadOnlyUnitOfWork()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string>());

        // 读到或没读到都算可达
        Assert.True((await Probe(root).ProbeAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal(new UnitOfWorkOptions { IsReadOnly = true }, unitOfWork.Options);
        Assert.NotEqual(UnitOfWorkState.Active, unitOfWork.State);
    }

    [Fact]
    public async Task Probe_ThrowingStore_SurfacesThatException_EvenIfRollbackAndDisposeThrow()
    {
        var failure = new TimeoutException(Secret);
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed."), DisposeFailure = new InvalidOperationException("The connection is still closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string> { Failure = failure });

        // 探针宿主按抛出的异常类型记 Degraded：要的是读表时的那个异常，不是回滚的
        var thrown = await Assert.ThrowsAsync<TimeoutException>(async () => await Probe(root).ProbeAsync(CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Probe_CallerCancellation_RollsBack_AndThrows()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<SchedulerLeaseEntity, string>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Probe(root).ProbeAsync(cancellation.Token));

        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Equal(CancellationToken.None, unitOfWork.Tokens[^1]);
    }

    [Fact]
    public async Task Invocation_ThrowingOutsideThePipeline_IsStoreUnavailable_WithTheExceptionTypeOnly()
    {
        var host = await _database.StartHostAsync();
        // 已释放的容器开不出作用域：异常发生在进入管道之前，没有经过内核的转换
        var root = new ServiceCollection().BuildServiceProvider();
        await root.DisposeAsync();

        var result = await Invocation(root, host.App).RunAsync(static _ => ValueTask.FromResult(Result.Success(1)), CancellationToken.None);

        Assert.Equal(SchedulingErrors.StoreUnavailable, result.Error?.Code);
        Assert.Equal(typeof(ObjectDisposedException).FullName, result.Error!.Details!["reason"]);
        Assert.DoesNotContain("disposed object", Flatten(result.Error), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(_log.Entries, static entry => entry.Level == LogLevel.Warning && entry.Exception is ObjectDisposedException);
    }

    [Fact]
    public async Task Invocation_WhenTheAppIsNotServing_IsStoreUnavailable_WithTheKernelCodeAsTheReason()
    {
        var host = await _database.StartHostAsync();
        await host.App.StopAsync(CancellationToken.None);

        var result = await Invocation(host.Services, host.App).RunAsync(static _ => ValueTask.FromResult(Result.Success(1)), CancellationToken.None);

        Assert.Equal(SchedulingErrors.StoreUnavailable, result.Error?.Code);
        Assert.Equal(KernelErrors.InvalidAppState, result.Error!.Details!["reason"]);
        Assert.Equal(KernelErrors.InvalidAppState, result.Error.Inner?.Code);
        // 应用不在服务状态是预期的状态，不是故障：不记日志
        Assert.Empty(_log.Entries);
    }

    /// <summary>
    /// 建在替身容器上的租约行操作
    /// </summary>
    private LeaseRows Rows(IServiceProvider root, App app) => new(root, app, TimeProvider.System, _log);

    /// <summary>
    /// 建在替身容器上的探针
    /// </summary>
    private LeaseProbe Probe(IServiceProvider root) => new(root, Resource, _log);

    /// <summary>
    /// 建在给定容器上的管道执行
    /// </summary>
    private StoreInvocation Invocation(IServiceProvider root, App app) => new(root, app, _log);

    /// <summary>
    /// 本节点持有的租约行
    /// </summary>
    private static SchedulerLeaseEntity Row() => new() { Id = Resource, Holder = "node-a", Token = Token, ExpiresAt = DateTimeOffset.UnixEpoch };

    /// <summary>
    /// 只装了工作单元与仓储替身的容器
    /// </summary>
    private static ServiceProvider Root(ScriptedUnitOfWork unitOfWork, ScriptedRepository<SchedulerLeaseEntity, string> repository)
        => new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ => unitOfWork)
            .AddScoped<IRepository<SchedulerLeaseEntity, string>>(_ => repository)
            .BuildServiceProvider();

    /// <summary>
    /// 错误连同内部错误里所有可能带出文本的地方
    /// </summary>
    private static string Flatten(Error error)
    {
        var parts = new List<string>();
        for (var current = error; current is not null; current = current.Inner)
        {
            parts.Add(current.Code);
            parts.Add(current.Message);
            parts.AddRange(current.Details?.Select(static pair => $"{pair.Key}={pair.Value}") ?? []);
        }

        return string.Join('\n', parts);
    }

    /// <summary>
    /// 把日志连同异常记在内存里
    /// </summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <summary>
        /// 收到的日志（级别、格式化后的消息、异常），按写入顺序
        /// </summary>
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
