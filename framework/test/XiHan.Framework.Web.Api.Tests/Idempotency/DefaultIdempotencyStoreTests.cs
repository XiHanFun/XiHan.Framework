// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 进程内幂等存储测试
/// </summary>
public sealed class DefaultIdempotencyStoreTests : IDisposable
{
    private static readonly IdempotencyRecordKey Key = new("", "42", "POST", "/api/orders", "k1");
    private readonly ManualTimeProvider _clock = new();
    private readonly ServiceProvider _provider = BuildUnitOfWorkProvider();

    /// <summary>
    /// 释放工作单元容器
    /// </summary>
    public void Dispose()
    {
        _provider.Dispose();
    }

    /// <summary>
    /// 事务型工作单元提交前同一键为处理中，提交后重播
    /// </summary>
    [Fact]
    public async Task CompleteInTransaction_VisibleOnlyAfterCommit()
    {
        var store = CreateStore();
        var manager = _provider.GetRequiredService<IUnitOfWorkManager>();
        var acquired = await store.TryAcquireAsync(Key, "fp", true);

        using (var unitOfWork = manager.Begin(new XiHanUnitOfWorkOptions { IsTransactional = true }))
        {
            await store.CompleteAsync(Key, acquired.OwnerToken, new StoredResponse(200, [1]));
            Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(Key, "fp", true)).Status);

            await unitOfWork.CompleteAsync();
        }

        Assert.Equal(IdempotencyAcquireStatus.Replay, (await store.TryAcquireAsync(Key, "fp", true)).Status);
    }

    /// <summary>
    /// 事务型工作单元未提交即释放后，外层释放记录，可重新取得
    /// </summary>
    [Fact]
    public async Task CompleteInTransaction_NotCommitted_ReleaseAllowsRetry()
    {
        var store = CreateStore();
        var manager = _provider.GetRequiredService<IUnitOfWorkManager>();
        var acquired = await store.TryAcquireAsync(Key, "fp", true);

        using (manager.Begin(new XiHanUnitOfWorkOptions { IsTransactional = true }))
        {
            await store.CompleteAsync(Key, acquired.OwnerToken, new StoredResponse(200, [1]));
        }

        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(Key, "fp", true)).Status);
        await store.ReleaseAsync(Key, acquired.OwnerToken);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, (await store.TryAcquireAsync(Key, "fp", true)).Status);
    }

    /// <summary>
    /// 非事务型工作单元内写入完成立即可重播
    /// </summary>
    [Fact]
    public async Task CompleteInNonTransactionalUnitOfWork_ReplaysImmediately()
    {
        var store = CreateStore();
        var manager = _provider.GetRequiredService<IUnitOfWorkManager>();
        var acquired = await store.TryAcquireAsync(Key, "fp", false);

        using (manager.Begin(new XiHanUnitOfWorkOptions { IsTransactional = false }))
        {
            await store.CompleteAsync(Key, acquired.OwnerToken, new StoredResponse(200, [1]));
            Assert.Equal(IdempotencyAcquireStatus.Replay, (await store.TryAcquireAsync(Key, "fp", false)).Status);
        }
    }

    private static ServiceProvider BuildUnitOfWorkProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<XiHanUnitOfWorkDefaultOptions>();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddSingleton<IUnitOfWorkTransactionBehaviourProvider, NullUnitOfWorkTransactionBehaviourProvider>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        return services.BuildServiceProvider();
    }

    private DefaultIdempotencyStore CreateStore(Action<XiHanIdempotencyOptions>? configure = null)
    {
        var options = new XiHanIdempotencyOptions();
        configure?.Invoke(options);
        return new DefaultIdempotencyStore(Options.Create(options), _clock, _provider.GetRequiredService<IUnitOfWorkManager>());
    }

    /// <summary>
    /// 首次取得成功，同内容再次取得为处理中
    /// </summary>
    [Fact]
    public async Task TryAcquire_SecondTimeWhileProcessing_ReturnsInProgress()
    {
        var store = CreateStore();

        var first = await store.TryAcquireAsync(Key, "fp", isTransactional: true);
        var second = await store.TryAcquireAsync(Key, "fp", isTransactional: true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, first.Status);
        Assert.NotEqual(Guid.Empty, first.OwnerToken);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, second.Status);
    }

    /// <summary>
    /// 相同键、不同内容为冲突
    /// </summary>
    [Fact]
    public async Task TryAcquire_DifferentFingerprint_ReturnsConflict()
    {
        var store = CreateStore();
        await store.TryAcquireAsync(Key, "fp-1", true);

        var result = await store.TryAcquireAsync(Key, "fp-2", true);

        Assert.Equal(IdempotencyAcquireStatus.Conflict, result.Status);
    }

    /// <summary>
    /// 完成后重播已保存的响应
    /// </summary>
    [Fact]
    public async Task TryAcquire_AfterComplete_ReturnsReplay()
    {
        var store = CreateStore();
        var acquired = await store.TryAcquireAsync(Key, "fp", true);
        var response = new StoredResponse(201, "{\"id\":1}"u8.ToArray());

        await store.CompleteAsync(Key, acquired.OwnerToken, response);
        var replay = await store.TryAcquireAsync(Key, "fp", true);

        Assert.Equal(IdempotencyAcquireStatus.Replay, replay.Status);
        Assert.Equal(201, replay.Response!.StatusCode);
        Assert.Equal(response.Body, replay.Response.Body);
    }

    /// <summary>
    /// 完成记录超过保留期后可重新取得
    /// </summary>
    [Fact]
    public async Task TryAcquire_CompletedExpired_ReturnsAcquired()
    {
        var store = CreateStore();
        var acquired = await store.TryAcquireAsync(Key, "fp", true);
        await store.CompleteAsync(Key, acquired.OwnerToken, new StoredResponse(200, null));

        _clock.Advance(TimeSpan.FromHours(24));
        var result = await store.TryAcquireAsync(Key, "fp-new", true);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, result.Status);
    }

    /// <summary>
    /// 处理中记录不自动过期，不确定记录超过保留期后可重新取得
    /// </summary>
    [Fact]
    public async Task Processing_NeverExpires_IndeterminateExpiresAfterRetention()
    {
        var store = CreateStore();
        var otherKey = Key with { Key = "k2" };
        await store.TryAcquireAsync(Key, "fp", true);
        var second = await store.TryAcquireAsync(otherKey, "fp", false);
        await store.MarkIndeterminateAsync(otherKey, second.OwnerToken);

        _clock.Advance(TimeSpan.FromHours(23));
        Assert.Equal(IdempotencyAcquireStatus.Indeterminate, (await store.TryAcquireAsync(otherKey, "fp", false)).Status);

        _clock.Advance(TimeSpan.FromDays(30));

        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(Key, "fp", true)).Status);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, (await store.TryAcquireAsync(otherKey, "fp-new", false)).Status);
    }

    /// <summary>
    /// 记录数满时清理过期的不确定记录腾出空间
    /// </summary>
    [Fact]
    public async Task TryAcquire_WhenFull_PurgesExpiredIndeterminate()
    {
        var store = CreateStore(options => options.MaxEntries = 1);
        var a = await store.TryAcquireAsync(Key with { Key = "a" }, "fp", false);
        await store.MarkIndeterminateAsync(Key with { Key = "a" }, a.OwnerToken);

        var full = await store.TryAcquireAsync(Key with { Key = "b" }, "fp", true);
        _clock.Advance(TimeSpan.FromHours(25));
        var afterExpiry = await store.TryAcquireAsync(Key with { Key = "b" }, "fp", true);

        Assert.Equal(IdempotencyAcquireStatus.CapacityExceeded, full.Status);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, afterExpiry.Status);
    }

    /// <summary>
    /// 清理过期完成记录后释放快照字节预算
    /// </summary>
    [Fact]
    public async Task TryAcquire_WhenBytesFull_PurgeReleasesByteBudget()
    {
        var store = CreateStore(options => options.MaxTotalResponseBytes = 4);
        var a = await store.TryAcquireAsync(Key, "fp", true);
        await store.CompleteAsync(Key, a.OwnerToken, new StoredResponse(200, [1, 2, 3, 4]));

        _clock.Advance(TimeSpan.FromHours(25));
        var k2 = Key with { Key = "k2" };
        var b = await store.TryAcquireAsync(k2, "fp", true);
        await store.CompleteAsync(k2, b.OwnerToken, new StoredResponse(200, [5, 6, 7, 8]));

        Assert.Equal(IdempotencyAcquireStatus.Acquired, b.Status);
        Assert.Equal(IdempotencyAcquireStatus.Replay, (await store.TryAcquireAsync(k2, "fp", true)).Status);
    }

    /// <summary>
    /// 释放后可重新取得，释放会撤掉已完成记录
    /// </summary>
    [Fact]
    public async Task Release_RemovesOwnedRecordInAnyState()
    {
        var store = CreateStore();
        var acquired = await store.TryAcquireAsync(Key, "fp", true);
        await store.CompleteAsync(Key, acquired.OwnerToken, new StoredResponse(200, null));

        await store.ReleaseAsync(Key, acquired.OwnerToken);

        Assert.Equal(IdempotencyAcquireStatus.Acquired, (await store.TryAcquireAsync(Key, "fp", true)).Status);
    }

    /// <summary>
    /// 令牌不符时释放不生效，完成抛出异常
    /// </summary>
    [Fact]
    public async Task WrongOwnerToken_IsRejected()
    {
        var store = CreateStore();
        await store.TryAcquireAsync(Key, "fp", true);

        await store.ReleaseAsync(Key, Guid.NewGuid());

        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(Key, "fp", true)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CompleteAsync(Key, Guid.NewGuid(), new StoredResponse(200, null)));
    }

    /// <summary>
    /// 记录数满时返回容量已满，不驱逐处理中记录；过期完成记录会被清理腾出空间
    /// </summary>
    [Fact]
    public async Task TryAcquire_WhenFull_ReturnsCapacityExceeded()
    {
        var store = CreateStore(options => options.MaxEntries = 2);
        var a = await store.TryAcquireAsync(Key with { Key = "a" }, "fp", true);
        await store.TryAcquireAsync(Key with { Key = "b" }, "fp", true);

        var full = await store.TryAcquireAsync(Key with { Key = "c" }, "fp", true);
        await store.CompleteAsync(Key with { Key = "a" }, a.OwnerToken, new StoredResponse(200, null));
        _clock.Advance(TimeSpan.FromHours(25));
        var afterExpiry = await store.TryAcquireAsync(Key with { Key = "c" }, "fp", true);

        Assert.Equal(IdempotencyAcquireStatus.CapacityExceeded, full.Status);
        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(Key with { Key = "b" }, "fp", true)).Status);
        Assert.Equal(IdempotencyAcquireStatus.Acquired, afterExpiry.Status);
    }

    /// <summary>
    /// 快照总字节达到上限时返回容量已满
    /// </summary>
    [Fact]
    public async Task TryAcquire_WhenResponseBytesFull_ReturnsCapacityExceeded()
    {
        var store = CreateStore(options => options.MaxTotalResponseBytes = 4);
        var a = await store.TryAcquireAsync(Key, "fp", true);
        await store.CompleteAsync(Key, a.OwnerToken, new StoredResponse(200, [1, 2, 3, 4]));

        var result = await store.TryAcquireAsync(Key with { Key = "k2" }, "fp", true);

        Assert.Equal(IdempotencyAcquireStatus.CapacityExceeded, result.Status);
    }

    /// <summary>
    /// 完成写入会使快照总字节超过上限时抛出异常，记录仍为处理中
    /// </summary>
    [Fact]
    public async Task CompleteAsync_WhenBytesWouldExceedCap_Throws()
    {
        var store = CreateStore(options => options.MaxTotalResponseBytes = 4);
        var k1 = Key with { Key = "k1" };
        var k2 = Key with { Key = "k2" };
        var a = await store.TryAcquireAsync(k1, "fp", true);
        var b = await store.TryAcquireAsync(k2, "fp", true);

        await store.CompleteAsync(k1, a.OwnerToken, new StoredResponse(200, [1, 2, 3]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CompleteAsync(k2, b.OwnerToken, new StoredResponse(200, [1, 2])));
        Assert.Equal(IdempotencyAcquireStatus.InProgress, (await store.TryAcquireAsync(k2, "fp", true)).Status);
    }

    /// <summary>
    /// 完成写入超限时先清理过期完成记录再判断
    /// </summary>
    [Fact]
    public async Task CompleteAsync_PurgesExpiredBeforeRejecting()
    {
        var store = CreateStore(options => options.MaxTotalResponseBytes = 4);
        var k1 = Key with { Key = "k1" };
        var k2 = Key with { Key = "k2" };
        var a = await store.TryAcquireAsync(k1, "fp", true);
        var b = await store.TryAcquireAsync(k2, "fp", true);
        await store.CompleteAsync(k1, a.OwnerToken, new StoredResponse(200, [1, 2, 3, 4]));
        _clock.Advance(TimeSpan.FromHours(25));

        await store.CompleteAsync(k2, b.OwnerToken, new StoredResponse(200, [5, 6, 7, 8]));

        Assert.Equal(IdempotencyAcquireStatus.Replay, (await store.TryAcquireAsync(k2, "fp", true)).Status);
    }

    /// <summary>
    /// 20 个并发取得只有一个成功
    /// </summary>
    [Fact]
    public async Task TryAcquire_Concurrent_OnlyOneAcquired()
    {
        var store = CreateStore();
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return await store.TryAcquireAsync(Key, "fp", true);
            }))
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r.Status == IdempotencyAcquireStatus.Acquired);
        Assert.All(results.Where(r => r.Status != IdempotencyAcquireStatus.Acquired),
            r => Assert.Equal(IdempotencyAcquireStatus.InProgress, r.Status));
    }
}
