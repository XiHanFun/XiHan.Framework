// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Auditing.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Auditing.Database.Tests.Fixtures;
using XiHan.Framework.Auditing.Database.Trail;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Database.Tests;

/// <summary>
/// 审计存储故障时的收尾与归类
/// </summary>
/// <remarks>
/// 回滚与释放作用域再失败时，查询返回的仍是原故障、落库日志里记的仍是原异常；查询的错误只带异常类型全名；调用方取消照常抛出，驱动以取消异常报告的超时是存储不可用（ADR-0259）
/// </remarks>
public sealed class DatabaseAuditTrailFaultTests : IDisposable
{
    /// <summary>
    /// 底层异常里不该外泄的文本
    /// </summary>
    private const string Secret = "Host=audit.internal;Password=hunter2";

    /// <summary>
    /// 收集日志的工厂
    /// </summary>
    private readonly LogCapture _logs = new();

    /// <inheritdoc />
    public void Dispose() => _logs.Dispose();

    [Fact]
    public async Task Query_WhoseRollbackThrows_ReportsTheOriginalFailure()
    {
        var failure = new TimeoutException(Secret);
        var rollback = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback };
        await using var root = Root(unitOfWork, new ScriptedRepository<AuditRecord, Guid> { Failure = failure });
        using var trail = Trail(root);

        var queried = await trail.QueryAsync(new AuditQuery(), CancellationToken.None);

        // reason 是读取时抛出的那个异常的类型，不是回滚的；异常消息不进错误
        Assert.Equal(AuditingErrors.TrailUnavailable, queried.Error?.Code);
        Assert.Equal(typeof(TimeoutException).FullName, queried.Error!.Details!["reason"]);
        Assert.DoesNotContain(Secret, Flatten(queried.Error), StringComparison.Ordinal);
        Assert.Contains(_logs.Entries, entry => entry.Message.Contains("Querying the audit trail", StringComparison.Ordinal) && ReferenceEquals(entry.Exception, failure));
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, rollback));
    }

    [Fact]
    public async Task Query_WhoseRollbackAndDisposeThrow_StillReturnsTrailUnavailable()
    {
        // 数据库断开：读取抛出，回滚再抛出，Provider 释放作用域时再回滚一次、再抛一次
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed."), DisposeFailure = new InvalidOperationException("The connection is still closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<AuditRecord, Guid> { Failure = new TimeoutException(Secret) });
        using var trail = Trail(root);

        var queried = await trail.QueryAsync(new AuditQuery(), CancellationToken.None);

        Assert.Equal(AuditingErrors.TrailUnavailable, queried.Error?.Code);
        Assert.Equal(typeof(TimeoutException).FullName, queried.Error!.Details!["reason"]);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Query_FailedCommit_IsTrailUnavailable_WithTheCommitErrorInside()
    {
        var conflict = Error.Literal(PersistenceErrors.Concurrency, "Concurrency conflict.");
        var unitOfWork = new ScriptedUnitOfWork { CommitResult = conflict };
        await using var root = Root(unitOfWork, new ScriptedRepository<AuditRecord, Guid>());
        using var trail = Trail(root);

        var queried = await trail.QueryAsync(new AuditQuery(), CancellationToken.None);

        // 提交返回的失败不是异常：以它的错误码为原因、原错误为内部错误
        Assert.Equal(AuditingErrors.TrailUnavailable, queried.Error?.Code);
        Assert.Equal(PersistenceErrors.Concurrency, queried.Error!.Details!["reason"]);
        Assert.Same(conflict, queried.Error.Inner);
    }

    [Fact]
    public async Task Query_CallerCancellation_RollsBack_AndThrows()
    {
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<AuditRecord, Guid>());
        using var trail = Trail(root);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await trail.QueryAsync(new AuditQuery(), cancellation.Token));

        Assert.Equal(cancellation.Token, thrown.CancellationToken);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Equal(CancellationToken.None, unitOfWork.Tokens[^1]);
    }

    [Fact]
    public async Task Query_TimeoutReportedAsCancellation_IsTrailUnavailable()
    {
        // 驱动把命令超时报告成取消异常，调用方的令牌并没有取消：查询接口不抛出，返回存储不可用
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<AuditRecord, Guid> { Failure = new TaskCanceledException(Secret) });
        using var trail = Trail(root);

        var queried = await trail.QueryAsync(new AuditQuery(), CancellationToken.None);

        Assert.Equal(AuditingErrors.TrailUnavailable, queried.Error?.Code);
        Assert.Equal(typeof(TaskCanceledException).FullName, queried.Error!.Details!["reason"]);
        Assert.DoesNotContain(Secret, Flatten(queried.Error), StringComparison.Ordinal);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Query_OpensAReadOnlyUnitOfWork_AndFlushAWritableOne()
    {
        var reading = new ScriptedUnitOfWork();
        await using (var root = Root(reading, new ScriptedRepository<AuditRecord, Guid>()))
        {
            using var trail = Trail(root);
            Assert.Empty((await trail.QueryAsync(new AuditQuery(), CancellationToken.None)).ValueOrThrow().Items);
        }

        Assert.Equal(new UnitOfWorkOptions { IsReadOnly = true }, reading.Options);
        Assert.Equal(["begin", "commit", "dispose"], reading.Calls);

        var writing = new ScriptedUnitOfWork();
        await using (var root = Root(writing, new ScriptedRepository<AuditRecord, Guid>()))
        {
            using var trail = Trail(root);
            await trail.WriteAsync(Event("orders.create"), CancellationToken.None);
            await trail.FlushAsync(CancellationToken.None);
            Assert.Equal((1L, 0L), (trail.PersistedCount, trail.FailedCount));
        }

        Assert.Equal(UnitOfWorkOptions.Default, writing.Options);
        Assert.Equal(["begin", "commit", "dispose"], writing.Calls);
    }

    [Fact]
    public async Task Flush_WhoseRollbackThrows_LogsTheOriginalFailure()
    {
        var failure = new TimeoutException(Secret);
        var rollback = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback };
        await using var root = Root(unitOfWork, new ScriptedRepository<AuditRecord, Guid> { Failure = failure });
        using var trail = Trail(root);
        await trail.WriteAsync(Event("orders.create"), CancellationToken.None);

        // 刷写不抛出：整批丢弃并计数
        await trail.FlushAsync(CancellationToken.None);

        Assert.Equal((0, 0L, 1L), (trail.PendingCount, trail.PersistedCount, trail.FailedCount));
        // 批失败的日志带的是写入时抛出的异常，回滚的异常另记一条
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("Persisting a batch", StringComparison.Ordinal) && ReferenceEquals(entry.Exception, failure));
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, rollback));
    }

    [Fact]
    public async Task Flush_FailedCommit_DropsTheBatch()
    {
        var unitOfWork = new ScriptedUnitOfWork { CommitResult = Error.Literal(PersistenceErrors.CapacityExceeded, "The store is full.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<AuditRecord, Guid>());
        using var trail = Trail(root);
        await trail.WriteAsync(Event("orders.create"), CancellationToken.None);

        await trail.FlushAsync(CancellationToken.None);

        // 提交返回失败与抛出一样是整批没落库
        Assert.Equal((0L, 1L), (trail.PersistedCount, trail.FailedCount));
        Assert.Contains(_logs.Entries, static entry => entry.Level == LogLevel.Error && entry.Exception is FrameworkException { Code: PersistenceErrors.CapacityExceeded });
    }

    [Fact]
    public async Task Flush_WhoseScopeDisposeThrowsAfterTheCommit_CountsTheBatchAsPersisted()
    {
        var dispose = new InvalidOperationException("The connection is closed.");
        var unitOfWork = new ScriptedUnitOfWork { DisposeFailure = dispose };
        await using var root = Root(unitOfWork, new ScriptedRepository<AuditRecord, Guid>());
        using var trail = Trail(root);
        await trail.WriteAsync(Event("orders.create"), CancellationToken.None);

        await trail.FlushAsync(CancellationToken.None);

        // 已经提交的一批不因为释放作用域失败被记成没落库
        Assert.Equal((1L, 0L), (trail.PersistedCount, trail.FailedCount));
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, dispose));
    }

    /// <summary>
    /// 建在替身容器上的审计存储
    /// </summary>
    private DatabaseAuditTrail Trail(IServiceProvider root)
        => new(root, new DatabaseAuditingOptions(), new GuidV7IdGenerator(TimeProvider.System), TimeProvider.System, _logs);

    /// <summary>
    /// 只装了工作单元与仓储替身的容器
    /// </summary>
    private static ServiceProvider Root(ScriptedUnitOfWork unitOfWork, ScriptedRepository<AuditRecord, Guid> repository)
        => new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ => unitOfWork)
            .AddScoped<IRepository<AuditRecord, Guid>>(_ => repository)
            .BuildServiceProvider();

    /// <summary>
    /// 一条成功的审计事件
    /// </summary>
    private static AuditEvent Event(string action) => new() { Timestamp = DateTimeOffset.UnixEpoch, Action = action, Outcome = Result.Success(), TraceId = "t" };

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
