// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 租户目录读数据库失败时的收尾与归类
/// </summary>
/// <remarks>
/// 数据库故障归为存储不可达且错误里没有异常文本；回滚与释放作用域再失败也不盖掉这个结果；调用方取消照常抛出，驱动以取消异常报告的超时是存储不可达（ADR-0259）
/// </remarks>
public sealed class CatalogInvocationTests : IDisposable
{
    /// <summary>
    /// 底层异常里不该外泄的文本
    /// </summary>
    private const string Secret = "Host=tenants.internal;Password=hunter2";

    /// <summary>
    /// 收集日志的工厂
    /// </summary>
    private readonly LogCapture _logs = new();

    /// <inheritdoc />
    public void Dispose() => _logs.Dispose();

    [Fact]
    public async Task ThrowingRead_IsStoreUnavailable_WithoutTheExceptionText()
    {
        var failure = new InvalidOperationException(Secret);
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<TenantRecord, string> { Failure = failure });

        var read = await ReadAsync(root, CancellationToken.None);

        Assert.Equal(MultiTenancyErrors.StoreUnavailable, read.Error?.Code);
        Assert.Null(read.Error!.Inner);
        Assert.DoesNotContain(Secret, Flatten(read.Error), StringComparison.Ordinal);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        // 完整异常只进日志
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, failure));
    }

    [Fact]
    public async Task FrameworkException_IsKeptAsTheInnerError()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<TenantRecord, string> { Failure = new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, "There is no active unit of work.") });

        var read = await ReadAsync(root, CancellationToken.None);

        Assert.Equal(MultiTenancyErrors.StoreUnavailable, read.Error?.Code);
        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, read.Error?.Inner?.Code);
    }

    [Fact]
    public async Task FailedRollbackAndDispose_DoNotReplaceTheStoreUnavailableResult()
    {
        // 数据库断开：读取抛出，回滚再抛出，Provider 释放作用域时再回滚一次、再抛一次
        var failure = new TimeoutException(Secret);
        var rollback = new InvalidOperationException("The connection is closed.");
        var dispose = new InvalidOperationException("The connection is still closed.");
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = rollback, DisposeFailure = dispose };
        await using var root = Root(unitOfWork, new ScriptedRepository<TenantRecord, string> { Failure = failure });

        var read = await ReadAsync(root, CancellationToken.None);

        Assert.Equal(MultiTenancyErrors.StoreUnavailable, read.Error?.Code);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        // 三个异常都在日志里：原异常、回滚的、释放的
        Assert.Contains(_logs.Entries, entry => ReferenceEquals(entry.Exception, failure));
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, rollback));
        Assert.Contains(_logs.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, dispose));
    }

    [Fact]
    public async Task FailedCommit_IsStoreUnavailable_WithTheCommitErrorInside()
    {
        var conflict = Error.Literal(PersistenceErrors.Concurrency, "Concurrency conflict.");
        var unitOfWork = new ScriptedUnitOfWork { CommitResult = conflict };
        await using var root = Root(unitOfWork, new ScriptedRepository<TenantRecord, string>());

        var read = await ReadAsync(root, CancellationToken.None);

        Assert.Equal(MultiTenancyErrors.StoreUnavailable, read.Error?.Code);
        Assert.Same(conflict, read.Error?.Inner);
        Assert.Contains(_logs.Entries, static entry => entry.Level == LogLevel.Warning && entry.Message.Contains(PersistenceErrors.Concurrency, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CallerCancellation_RollsBack_AndThrows()
    {
        var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed.") };
        await using var root = Root(unitOfWork, new ScriptedRepository<TenantRecord, string>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // 调用方取消不是存储故障：回滚（它再失败也只记日志）后按异步约定抛出
        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ReadAsync(root, cancellation.Token));

        Assert.Equal(cancellation.Token, thrown.CancellationToken);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
        Assert.Equal(CancellationToken.None, unitOfWork.Tokens[^1]);
    }

    [Fact]
    public async Task TimeoutReportedAsCancellation_IsStoreUnavailable()
    {
        // 驱动把命令超时报告成取消异常，调用方的令牌并没有取消：读不到不能当成不存在，也不能当成调用方取消
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<TenantRecord, string> { Failure = new TaskCanceledException(Secret) });

        var read = await ReadAsync(root, CancellationToken.None);

        Assert.Equal(MultiTenancyErrors.StoreUnavailable, read.Error?.Code);
        Assert.DoesNotContain(Secret, Flatten(read.Error!), StringComparison.Ordinal);
        Assert.Equal(["begin", "rollback", "dispose"], unitOfWork.Calls);
    }

    [Fact]
    public async Task Success_CommitsTheReadOnlyUnitOfWorkOnThePrimary()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        await using var root = Root(unitOfWork, new ScriptedRepository<TenantRecord, string> { Found = new TenantRecord { Id = "acme" } });

        var read = await ReadAsync(root, CancellationToken.None);

        Assert.Equal("acme", Assert.Single(read.ValueOrThrow()).Id);
        Assert.Equal(new UnitOfWorkOptions { IsReadOnly = true, ReadsPrimary = true }, unitOfWork.Options);
        Assert.Equal(["begin", "commit", "dispose"], unitOfWork.Calls);
        Assert.Empty(_logs.Entries);
    }

    /// <summary>
    /// 经目录的读取方式按 ID 读一次
    /// </summary>
    private ValueTask<Result<IReadOnlyList<TenantRecord>>> ReadAsync(IServiceProvider root, CancellationToken cancellationToken)
        => new CatalogInvocation(root, _logs.CreateLogger(nameof(CatalogInvocation)))
            .ReadAsync((repository, ct) => new ValueTask<IReadOnlyList<TenantRecord>>(repository.FindManyAsync(["acme"], ct)), cancellationToken);

    /// <summary>
    /// 只装了工作单元与仓储替身的容器
    /// </summary>
    private static ServiceProvider Root(ScriptedUnitOfWork unitOfWork, ScriptedRepository<TenantRecord, string> repository)
        => new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ => unitOfWork)
            .AddScoped<IRepository<TenantRecord, string>>(_ => repository)
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
