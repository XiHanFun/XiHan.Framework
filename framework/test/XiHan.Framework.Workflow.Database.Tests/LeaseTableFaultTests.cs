// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Database.Entities;
using XiHan.Framework.Workflow.Database.Locking;
using XiHan.Framework.Workflow.Database.Polling;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 租约表故障时租约步骤与探针的收尾与归类
/// </summary>
/// <remarks>
/// 回滚与释放作用域再失败也不盖掉已经确定的结果或原异常；存储不可达的原因只写异常类型全名（框架异常写它的错误码），异常消息不进错误；调用方取消回滚后抛出，驱动以取消异常报告的超时是存储不可达（ADR-0259）
/// </remarks>
public sealed class LeaseTableFaultTests : IDisposable
{
    /// <summary>
    /// 底层异常里不该外泄的文本
    /// </summary>
    private const string Secret = "Host=leases.internal;Password=hunter2";

    /// <summary>
    /// 租约行的主键
    /// </summary>
    private const string Id = "instance-1";

    /// <summary>
    /// 本次获取的持有者标记
    /// </summary>
    private const string Holder = "node-a:1";

    /// <summary>
    /// 租约时长
    /// </summary>
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 被测对象写入的日志
    /// </summary>
    private readonly LogCapture _logs = new();

    [Fact]
    public async Task Steps_RunInAWritableUnitOfWorkBoundToTheLockConnection()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string>());

        var attempt = await Table(root, "leases").TryAcquireAsync(Id, Holder, _lease, CancellationToken.None);

        Assert.Equal(LeaseAttempt.Acquired, attempt.ValueOrThrow());
        Assert.Equal(new UnitOfWorkOptions { Connection = "leases" }, unitOfWork.Options);
        Assert.Equal(["begin", "commit", "dispose"], unitOfWork.Calls);
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public async Task TryAcquire_WhoseInsertFails_ReturnsThatFailure_EvenIfTheRollbackThrows()
    {
        var full = Error.Literal(PersistenceErrors.CapacityExceeded, "The store is full.");
        var rollback = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback };
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string> { WriteResult = full });

        var attempt = await Table(root).TryAcquireAsync(Id, Holder, _lease, CancellationToken.None);

        // 步骤自己的失败原样返回，由实例锁归类；回滚的异常只进日志
        Assert.Same(full, attempt.Error);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, rollback));
    }

    [Fact]
    public async Task TryAcquire_FailedCommit_IsReturnedAsIs()
    {
        var conflict = Error.Literal(PersistenceErrors.Concurrency, "Concurrency conflict.");
        var unitOfWork = new ScriptedUnitOfWork { CommitResult = conflict };
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string>());

        var attempt = await Table(root).TryAcquireAsync(Id, Holder, _lease, CancellationToken.None);

        // 提交失败（写入阶段的标识重复、并发戳冲突）如实返回，由调用方按竞争处理
        Assert.Same(conflict, attempt.Error);
    }

    [Fact]
    public async Task TryAcquire_ThrowingStore_IsStoreUnavailable_WithTheExceptionTypeOnly()
    {
        var failure = new TimeoutException(Secret);
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string> { Failure = failure });

        var attempt = await Table(root).TryAcquireAsync(Id, Holder, _lease, CancellationToken.None);

        Assert.Equal(WorkflowErrors.StoreUnavailable, attempt.Error?.Code);
        Assert.Equal(typeof(TimeoutException).FullName, attempt.Error!.Details!["reason"]);
        Assert.DoesNotContain(Secret, Flatten(attempt.Error), StringComparison.Ordinal);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        // 完整异常只进日志
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, failure));
    }

    [Fact]
    public async Task TryAcquire_ThrowingStore_IsStillStoreUnavailable_WhenRollbackAndDisposeThrow()
    {
        // 数据库断开：读行抛出，回滚再抛出，Provider 释放作用域时再回滚一次、再抛一次
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed."), DisposeFailure = new InvalidOperationException("The connection is still closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string> { Failure = new TimeoutException(Secret) });

        var attempt = await Table(root).TryAcquireAsync(Id, Holder, _lease, CancellationToken.None);

        // 实例锁把存储不可达当可重试的失败：变成异常就绕过了等锁超时
        Assert.Equal(WorkflowErrors.StoreUnavailable, attempt.Error?.Code);
        Assert.Equal(typeof(TimeoutException).FullName, attempt.Error!.Details!["reason"]);
    }

    [Fact]
    public async Task TryAcquire_FrameworkException_IsStoreUnavailable_WithItsCodeAsTheReason()
    {
        await using var root = Root(new ScriptedUnitOfWork(), new ScriptedRepository<WorkflowLockRecord, string> { Failure = new FrameworkException(PersistenceErrors.UnknownConnection, "The unit of work is bound to a connection that is not declared.") });

        var attempt = await Table(root, "leases").TryAcquireAsync(Id, Holder, _lease, CancellationToken.None);

        Assert.Equal(WorkflowErrors.StoreUnavailable, attempt.Error?.Code);
        Assert.Equal(PersistenceErrors.UnknownConnection, attempt.Error!.Details!["reason"]);
        Assert.Equal(PersistenceErrors.UnknownConnection, attempt.Error.Inner?.Code);
    }

    [Fact]
    public async Task Renew_CallerCancellation_RollsBack_AndThrows_EvenIfTheRollbackThrows()
    {
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string> { Found = Row() });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Table(root).RenewAsync(Id, Holder, _lease, cancellation.Token));

        Assert.Equal(cancellation.Token, thrown.CancellationToken);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Equal(CancellationToken.None, unitOfWork.Tokens[^1]);
    }

    [Fact]
    public async Task Renew_TimeoutReportedAsCancellation_IsStoreUnavailable()
    {
        // 驱动把命令超时报告成取消异常，调用方的令牌并没有取消：与别的存储故障一样可重试
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string> { Failure = new TaskCanceledException(Secret) });

        var renewed = await Table(root).RenewAsync(Id, Holder, _lease, CancellationToken.None);

        Assert.Equal(WorkflowErrors.StoreUnavailable, renewed.Error?.Code);
        Assert.Equal(typeof(TaskCanceledException).FullName, renewed.Error!.Details!["reason"]);
        Assert.DoesNotContain(Secret, Flatten(renewed.Error), StringComparison.Ordinal);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Release_WhenTheRowIsGone_Succeeds_EvenIfDisposingTheScopeThrows()
    {
        var dispose = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { DisposeFailure = dispose };
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string>());

        var released = await Table(root).ReleaseAsync(Id, Holder, CancellationToken.None);

        Assert.True(released.IsSuccess, released.Error?.Code);
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, dispose));
    }

    [Fact]
    public async Task Release_WhoseRemoveFails_ReturnsThatFailure_EvenIfTheRollbackThrows()
    {
        var full = Error.Literal(PersistenceErrors.CapacityExceeded, "The store is full.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string> { Found = Row(), WriteResult = full });

        var released = await Table(root).ReleaseAsync(Id, Holder, CancellationToken.None);

        Assert.Same(full, released.Error);
    }

    [Fact]
    public async Task Probe_ReadsTheRowInAReadOnlyUnitOfWorkBoundToTheLockConnection()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string>());

        // 读到或没读到都算可达
        Assert.True((await Probe(root, "leases").ProbeAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal(new UnitOfWorkOptions { IsReadOnly = true, Connection = "leases" }, unitOfWork.Options);
        Assert.NotEqual(UnitOfWorkState.Active, unitOfWork.State);
    }

    [Fact]
    public async Task Probe_ThrowingStore_SurfacesThatException_EvenIfRollbackAndDisposeThrow()
    {
        var failure = new TimeoutException(Secret);
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed."), DisposeFailure = new InvalidOperationException("The connection is still closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string> { Failure = failure });

        // 探针宿主按抛出的异常类型记 Degraded：要的是读表时的那个异常，不是回滚的
        var thrown = await Assert.ThrowsAsync<TimeoutException>(async () => await Probe(root, null).ProbeAsync(CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Probe_CallerCancellation_RollsBack_AndThrows()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<WorkflowLockRecord, string>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Probe(root, null).ProbeAsync(cancellation.Token));

        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Equal(CancellationToken.None, unitOfWork.Tokens[^1]);
    }

    /// <inheritdoc />
    public void Dispose() => _logs.Dispose();

    /// <summary>
    /// 建在替身容器上的租约行操作
    /// </summary>
    private LeaseTable Table(IServiceProvider root, string? lockConnection = null) => new(root, TimeProvider.System, lockConnection, _logs.CreateLogger(nameof(LeaseTableFaultTests)));

    /// <summary>
    /// 建在替身容器上的探针
    /// </summary>
    private PollerLeaseProbe Probe(IServiceProvider root, string? lockConnection) => new(root, new DatabaseWorkflowOptions { LockConnection = lockConnection }, _logs.CreateLogger(nameof(LeaseTableFaultTests)));

    /// <summary>
    /// 本次获取持有的租约行
    /// </summary>
    private static WorkflowLockRecord Row() => new() { Id = Id, Holder = Holder, ExpiresAt = DateTimeOffset.MaxValue };

    /// <summary>
    /// 只装了工作单元与仓储替身的容器
    /// </summary>
    private static ServiceProvider Root(ScriptedUnitOfWork unitOfWork, ScriptedRepository<WorkflowLockRecord, string> repository)
        => new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ => unitOfWork)
            .AddScoped<IRepository<WorkflowLockRecord, string>>(_ => repository)
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
}
