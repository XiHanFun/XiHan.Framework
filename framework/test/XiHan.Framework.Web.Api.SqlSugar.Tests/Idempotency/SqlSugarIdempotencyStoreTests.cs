// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Web.Api.Idempotency;
using XiHan.Framework.Web.Api.SqlSugar.Entities;
using XiHan.Framework.Web.Api.SqlSugar.Idempotency;

namespace XiHan.Framework.Web.Api.SqlSugar.Tests.Idempotency;

/// <summary>
/// SqlSugar 幂等存储测试
/// </summary>
public sealed class SqlSugarIdempotencyStoreTests : IDisposable
{
    private readonly IdempotencyStoreTestContext _context = new();

    /// <summary>
    /// 释放夹具
    /// </summary>
    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// 首次取得成功，同内容再取为处理中，不同内容为冲突
    /// </summary>
    [Fact]
    public async Task 首次取得成功_同内容再取为处理中_不同内容为冲突()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");

        var first = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);
        var second = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);
        var conflict = await store.TryAcquireAsync(key, "fp-b", isTransactional: true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, first.Status);
        Assert.NotEqual(Guid.Empty, first.OwnerToken);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, second.Status);
        Assert.Equal(IdempotencyAcquireStatus.Conflict, conflict.Status);
    }

    /// <summary>
    /// 完成后重播状态码与快照
    /// </summary>
    [Fact]
    public async Task 完成后重播状态码与快照()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");
        var body = new byte[] { 1, 2, 3, 250 };

        var acquired = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);
        await store.CompleteAsync(key, acquired.OwnerToken, new StoredResponse(201, body));
        var replay = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);

        Assert.Equal(IdempotencyAcquireStatus.Replay, replay.Status);
        Assert.Equal(201, replay.Response!.StatusCode);
        Assert.Equal(body, replay.Response.Body);
    }

    /// <summary>
    /// 完成记录过期后可重新取得，且不同内容也可取得
    /// </summary>
    [Fact]
    public async Task 完成记录过期后可重新取得_且不同内容也可取得()
    {
        var store = _context.CreateStore();
        var sameKey = CreateKey("same");
        var otherKey = CreateKey("other");
        await CompleteAsync(store, sameKey, "fp-a");
        await CompleteAsync(store, otherKey, "fp-a");

        _context.Clock.Advance(_context.Options.CompletedRetention - TimeSpan.FromMilliseconds(1));
        Assert.Equal(IdempotencyAcquireStatus.Replay, (await store.TryAcquireAsync(sameKey, "fp-a", true)).Status);

        _context.Clock.Advance(TimeSpan.FromMilliseconds(2));
        var same = await store.TryAcquireAsync(sameKey, "fp-a", true);
        var other = await store.TryAcquireAsync(otherKey, "fp-b", true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, same.Status);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, other.Status);
        Assert.Equal("fp-b", _context.FindRecord(otherKey)!.Fingerprint);
        Assert.Equal(SysIdempotencyRecord.StatusProcessing, _context.FindRecord(otherKey)!.Status);
    }

    /// <summary>
    /// 事务型处理中记录租约过期后可被接管，旧令牌写入完成抛出异常
    /// </summary>
    [Fact]
    public async Task 事务型处理中租约过期后可接管_旧令牌完成抛出异常()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");
        var first = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);

        _context.Clock.Advance(_context.Options.ProcessingLease - TimeSpan.FromMilliseconds(1));
        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(key, "fp-a", true)).Status);

        _context.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var takeover = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, takeover.Status);
        Assert.NotEqual(first.OwnerToken, takeover.OwnerToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CompleteAsync(key, first.OwnerToken, new StoredResponse(200, null)));

        await store.CompleteAsync(key, takeover.OwnerToken, new StoredResponse(200, null));
        var replay = await store.TryAcquireAsync(key, "fp-a", true);
        Assert.Equal(IdempotencyAcquireStatus.Replay, replay.Status);
        Assert.Null(replay.Response!.Body);
    }

    /// <summary>
    /// 非事务型处理中记录租约过期后仍为处理中
    /// </summary>
    [Fact]
    public async Task 非事务型处理中租约过期后仍为处理中()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");
        await store.TryAcquireAsync(key, "fp-a", isTransactional: false);

        _context.Clock.Advance(_context.Options.ProcessingLease + TimeSpan.FromHours(1));
        var result = await store.TryAcquireAsync(key, "fp-a", isTransactional: false);

        Assert.Equal(IdempotencyAcquireStatus.InProgress, result.Status);
    }

    /// <summary>
    /// 标记不确定后返回不确定
    /// </summary>
    [Fact]
    public async Task 标记不确定后返回不确定()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");
        var acquired = await store.TryAcquireAsync(key, "fp-a", isTransactional: false);

        await store.MarkIndeterminateAsync(key, acquired.OwnerToken);
        var result = await store.TryAcquireAsync(key, "fp-a", isTransactional: false);

        Assert.Equal(IdempotencyAcquireStatus.Indeterminate, result.Status);
        Assert.Equal(SysIdempotencyRecord.StatusIndeterminate, _context.FindRecord(key)!.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CompleteAsync(key, acquired.OwnerToken, new StoredResponse(200, null)));
    }

    /// <summary>
    /// 不确定记录超过保留期后可重新取得
    /// </summary>
    [Fact]
    public async Task 不确定记录过期后可重新取得()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");
        var acquired = await store.TryAcquireAsync(key, "fp-a", isTransactional: false);
        await store.MarkIndeterminateAsync(key, acquired.OwnerToken);

        _context.Clock.Advance(_context.Options.CompletedRetention - TimeSpan.FromMilliseconds(1));
        Assert.Equal(IdempotencyAcquireStatus.Indeterminate, (await store.TryAcquireAsync(key, "fp-a", false)).Status);

        _context.Clock.Advance(TimeSpan.FromMilliseconds(2));
        var result = await store.TryAcquireAsync(key, "fp-b", isTransactional: false);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.Equal(SysIdempotencyRecord.StatusProcessing, _context.FindRecord(key)!.Status);
        Assert.Equal("fp-b", _context.FindRecord(key)!.Fingerprint);
    }

    /// <summary>
    /// 请求路径超过端点列长度时截断写入，仍可正常取得
    /// </summary>
    [Fact]
    public async Task 超长路径截断写入_仍可正常取得()
    {
        var store = _context.CreateStore();
        var key = new IdempotencyRecordKey(string.Empty, "42", "POST", "/api/" + new string('a', 600), "k1");

        var result = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.Equal(512, _context.FindRecord(key)!.Endpoint.Length);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(key, "fp-a", true)).Status);
    }

    /// <summary>
    /// 插入成功后请求被取消，取得结果仍为成功且记录保留
    /// </summary>
    [Fact]
    public async Task 插入成功后取消_仍返回取得且记录保留()
    {
        using var provider = BuildCancellationObservingProvider();
        using var cancellation = new CancellationTokenSource();
        using var client = _context.CreateClient();
        client.Aop.OnLogExecuted = (sql, _) =>
        {
            if (sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                cancellation.Cancel();
            }
        };
        var store = new SqlSugarIdempotencyStore(
            new StubClientResolver(client),
            provider.GetRequiredService<IUnitOfWorkManager>(),
            Options.Create(_context.Options),
            _context.Clock);
        var key = CreateKey("k1");

        var result = await store.TryAcquireAsync(key, "fp-a", isTransactional: true, cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
        Assert.Equal(result.OwnerToken, _context.FindRecord(key)!.OwnerToken);
    }

    /// <summary>
    /// 释放只删除处理中记录，完成记录不受影响
    /// </summary>
    [Fact]
    public async Task 释放只删除处理中记录_完成记录不受影响()
    {
        var store = _context.CreateStore();
        var processingKey = CreateKey("processing");
        var completedKey = CreateKey("completed");

        var processing = await store.TryAcquireAsync(processingKey, "fp-a", true);
        await store.ReleaseAsync(processingKey, Guid.NewGuid());
        Assert.NotNull(_context.FindRecord(processingKey));

        await store.ReleaseAsync(processingKey, processing.OwnerToken);
        Assert.Null(_context.FindRecord(processingKey));
        Assert.Equal(IdempotencyAcquireStatus.Acquired, (await store.TryAcquireAsync(processingKey, "fp-a", true)).Status);

        var completedToken = await CompleteAsync(store, completedKey, "fp-a");
        await store.ReleaseAsync(completedKey, completedToken);
        Assert.Equal(IdempotencyAcquireStatus.Replay, (await store.TryAcquireAsync(completedKey, "fp-a", true)).Status);
    }

    /// <summary>
    /// 业务回滚后完成状态不存在，租约过期后可安全重试
    /// </summary>
    [Fact]
    public async Task 业务回滚后完成状态不存在_租约过期后可安全重试()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");
        var acquired = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);

        _context.Client.Ado.BeginTran();
        await store.CompleteAsync(key, acquired.OwnerToken, new StoredResponse(200, [9, 9]));
        _context.Client.Ado.RollbackTran();

        var record = _context.FindRecord(key)!;
        Assert.Equal(SysIdempotencyRecord.StatusProcessing, record.Status);
        Assert.Null(record.ResponseBody);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(key, "fp-a", true)).Status);

        _context.Clock.Advance(_context.Options.ProcessingLease + TimeSpan.FromSeconds(1));
        var retry = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, retry.Status);
        Assert.NotEqual(acquired.OwnerToken, retry.OwnerToken);
    }

    /// <summary>
    /// 取得在外层事务回滚后仍然保留
    /// </summary>
    [Fact]
    public async Task 取得在外层事务回滚后仍然保留()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");
        var timeout = TimeSpan.FromSeconds(10);

        _context.Client.Ado.Open();
        var transaction = ((SqliteConnection)_context.Client.Ado.Connection).BeginTransaction(deferred: true);
        _context.Client.Ado.Transaction = transaction;
        IdempotencyAcquireResult acquired;
        try
        {
            acquired = await store.TryAcquireAsync(key, "fp-a", isTransactional: true).WaitAsync(timeout);
        }
        finally
        {
            transaction.Rollback();
            _context.Client.Ado.Transaction = null;
            _context.Client.Ado.Close();
        }

        Assert.Equal(IdempotencyAcquireStatus.Acquired, acquired.Status);
        using var freshClient = _context.CreateClient();
        var keyHash = key.ComputeHash();
        var record = freshClient.Queryable<SysIdempotencyRecord>().Where(item => item.KeyHash == keyHash).First();
        Assert.NotNull(record);
        Assert.Equal(SysIdempotencyRecord.StatusProcessing, record.Status);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(key, "fp-a", true).WaitAsync(timeout)).Status);
    }

    /// <summary>
    /// 业务提交后完成状态与快照同时存在并可重播
    /// </summary>
    [Fact]
    public async Task 业务提交后完成状态与快照同时存在并可重播()
    {
        var store = _context.CreateStore();
        var key = CreateKey("k1");
        var body = new byte[] { 7, 8, 9 };
        var acquired = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);

        _context.Client.Ado.BeginTran();
        await store.CompleteAsync(key, acquired.OwnerToken, new StoredResponse(202, body));
        _context.Client.Ado.CommitTran();

        var replay = await store.TryAcquireAsync(key, "fp-a", isTransactional: true);

        Assert.Equal(IdempotencyAcquireStatus.Replay, replay.Status);
        Assert.Equal(202, replay.Response!.StatusCode);
        Assert.Equal(body, replay.Response.Body);
    }

    /// <summary>
    /// 两个独立连接并发取得同一键只有一个成功
    /// </summary>
    [Fact]
    public async Task 两个独立连接并发取得同一键只有一个成功()
    {
        using var firstClient = _context.CreateScopeClient();
        using var secondClient = _context.CreateScopeClient();
        var stores = new[] { _context.CreateStore(firstClient), _context.CreateStore(secondClient) };
        var key = CreateKey("concurrent");
        using var gate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 20)
            .Select(index => Task.Factory.StartNew(() =>
            {
                gate.Wait();
                return stores[index % 2].TryAcquireAsync(key, "fp-a", isTransactional: true).GetAwaiter().GetResult();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            .ToArray();
        gate.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, result => result.Status == IdempotencyAcquireStatus.Acquired);
        Assert.Equal(19, results.Count(result => result.Status == IdempotencyAcquireStatus.InProgress));
        Assert.Equal(1, await _context.Client.Queryable<SysIdempotencyRecord>().CountAsync());
    }

    /// <summary>
    /// 清理删除过期的完成记录与不确定记录
    /// </summary>
    [Fact]
    public async Task 清理删除过期完成记录与不确定记录()
    {
        var store = _context.CreateStore();
        var expiredKey = CreateKey("expired");
        var freshKey = CreateKey("fresh");
        var processingKey = CreateKey("processing");
        var indeterminateKey = CreateKey("indeterminate");

        await CompleteAsync(store, expiredKey, "fp-a");
        await store.TryAcquireAsync(processingKey, "fp-a", true);
        var indeterminate = await store.TryAcquireAsync(indeterminateKey, "fp-a", false);
        await store.MarkIndeterminateAsync(indeterminateKey, indeterminate.OwnerToken);
        _context.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await CompleteAsync(store, freshKey, "fp-a");

        _context.Clock.Advance(_context.Options.CompletedRetention - TimeSpan.FromMilliseconds(250));
        var deleted = await store.PurgeExpiredAsync();

        Assert.Equal(2, deleted);
        Assert.Null(_context.FindRecord(expiredKey));
        Assert.Null(_context.FindRecord(indeterminateKey));
        Assert.NotNull(_context.FindRecord(freshKey));
        Assert.NotNull(_context.FindRecord(processingKey));
        Assert.Equal(2, await _context.Client.Queryable<SysIdempotencyRecord>().CountAsync());
    }

    private static ServiceProvider BuildCancellationObservingProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<XiHanUnitOfWorkDefaultOptions>();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddSingleton<IUnitOfWorkTransactionBehaviourProvider, NullUnitOfWorkTransactionBehaviourProvider>();
        services.AddTransient<IUnitOfWork, CancellationObservingUnitOfWork>();
        return services.BuildServiceProvider();
    }

    private static IdempotencyRecordKey CreateKey(string key)
    {
        return new IdempotencyRecordKey(string.Empty, "42", "POST", "/api/orders", key);
    }

    private static async Task<Guid> CompleteAsync(IIdempotencyStore store, IdempotencyRecordKey key, string fingerprint)
    {
        var acquired = await store.TryAcquireAsync(key, fingerprint, true);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, acquired.Status);
        await store.CompleteAsync(key, acquired.OwnerToken, new StoredResponse(200, [1]));
        return acquired.OwnerToken;
    }

    /// <summary>
    /// 提交时检查取消令牌的工作单元
    /// </summary>
    private sealed class CancellationObservingUnitOfWork(
        IServiceProvider serviceProvider,
        IUnitOfWorkEventPublisher unitOfWorkEventPublisher,
        IOptions<XiHanUnitOfWorkDefaultOptions> options,
        ILogger<UnitOfWork> logger) : UnitOfWork(serviceProvider, unitOfWorkEventPublisher, options, logger)
    {
        public override Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return base.CompleteAsync(cancellationToken);
        }
    }
}
