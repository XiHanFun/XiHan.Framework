// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="LeasedWorkerIdSource"/>。
/// </summary>
/// <remarks>
/// 多个实例共用一个锁存储时取最小空位、本地到期、续约保持机器位、空位被抢后重新分配、存储不可达按丢失处理、机器位全被占时启动失败、后台循环按周期续约。
/// </remarks>
public sealed class LeasedWorkerIdSourceTests
{
    /// <summary>
    /// 用例的起始时刻。
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 租约存活时间。
    /// </summary>
    /// <remarks>
    /// 续约间隔是它的三分之一。
    /// </remarks>
    private static readonly TimeSpan _timeToLive = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task StartAsync_TakesLowestFreeWorkerId_AcrossInstancesSharingTheLockStore()
    {
        var clock = new FakeTimeProvider(_start);
        var store = Store(clock);
        await using var first = Create(store, clock);
        await using var second = Create(store, clock);

        await first.StartAsync(CancellationToken.None);
        await second.StartAsync(CancellationToken.None);
        Assert.Equal(0, first.Current.ValueOrThrow());
        Assert.Equal(1, second.Current.ValueOrThrow());
        Assert.Equal(_start + _timeToLive, first.ExpiresAt);

        // 第一个实例停掉释放 0，新实例拿到 0
        await first.StopAsync(CancellationToken.None);
        Assert.False(first.Current.HasValue);
        await using var third = Create(store, clock);
        await third.StartAsync(CancellationToken.None);
        Assert.Equal(0, third.Current.ValueOrThrow());
    }

    [Fact]
    public async Task Current_ExpiresLocally_WhenNotRenewed_AndHealthTurnsDegraded()
    {
        var clock = new FakeTimeProvider(_start);
        await using var source = Create(Store(clock), clock);
        // 直接续约一次取得机器位，不起后台循环：到期行为只由本地时钟决定
        await source.RenewAsync(CancellationToken.None);
        Assert.Equal(0, source.Current.ValueOrThrow());
        Assert.Equal(LifecycleState.Ready, Assert.Single(source.Check()).State);

        clock.Advance(_timeToLive - TimeSpan.FromSeconds(1));
        Assert.True(source.Current.HasValue);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(source.Current.HasValue);
        var status = Assert.Single(source.Check());
        Assert.Equal(LeasedWorkerIdSource.HealthName, status.Name);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(PersistenceErrors.WorkerIdUnavailable, status.Error!.Code);
        Assert.Equal(PersistenceMessages.WorkerIdUnavailable, status.Error.MessageKey);
    }

    [Fact]
    public async Task RenewAsync_ExtendsLease_AndKeepsWorkerId()
    {
        var clock = new FakeTimeProvider(_start);
        var store = Store(clock);
        await using var source = Create(store, clock);
        await source.RenewAsync(CancellationToken.None);

        clock.Advance(source.RenewInterval);
        await source.RenewAsync(CancellationToken.None);

        Assert.Equal(0, source.Current.ValueOrThrow());
        Assert.Equal(_start + source.RenewInterval + _timeToLive, source.ExpiresAt);
        // 续约后别的实例仍拿不到 0
        await using var other = Create(store, clock);
        await other.StartAsync(CancellationToken.None);
        Assert.Equal(1, other.Current.ValueOrThrow());
    }

    [Fact]
    public async Task RenewAsync_ReallocatesWorkerId_WhenTheSlotWasTaken()
    {
        var clock = new FakeTimeProvider(_start);
        var store = Store(clock);
        await using var source = Create(store, clock);
        await source.RenewAsync(CancellationToken.None);

        // 模拟存储侧租约到期后被别的实例拿走：删掉锁条目，让另一个实例取得 0
        Assert.True(await store.RemoveAsync(LockKey(0), CancellationToken.None));
        await using var intruder = Create(store, clock);
        await intruder.StartAsync(CancellationToken.None);
        Assert.Equal(0, intruder.Current.ValueOrThrow());

        await source.RenewAsync(CancellationToken.None);

        Assert.Equal(1, source.Current.ValueOrThrow());
        Assert.Equal(0, intruder.Current.ValueOrThrow());
    }

    [Fact]
    public async Task RenewAsync_TreatsStoreFailureAsLost_AndReacquiresWhenTheStoreReturns()
    {
        var clock = new FakeTimeProvider(_start);
        var store = new SwitchableCacheStore(Store(clock));
        await using var source = Create(store, clock);
        await source.RenewAsync(CancellationToken.None);
        Assert.True(source.Current.HasValue);

        store.Fail = true;
        await source.RenewAsync(CancellationToken.None);
        Assert.False(source.Current.HasValue);
        Assert.Equal(LifecycleState.Degraded, Assert.Single(source.Check()).State);

        // 存储恢复：机器位 0 的锁条目还留在存储里（本实例的句柄已失效，只能等它在存储侧到期），所以重新分配拿到的是 1
        store.Fail = false;
        await source.RenewAsync(CancellationToken.None);
        Assert.Equal(1, source.Current.ValueOrThrow());
        Assert.Equal(LifecycleState.Ready, Assert.Single(source.Check()).State);

        // 旧条目到期后，再来的实例才能拿回 0：先把本实例的 1 续到更晚，再推进到 0 的旧条目到期（1 仍有效）
        clock.Advance(source.RenewInterval);
        await source.RenewAsync(CancellationToken.None);
        clock.Advance(_timeToLive - source.RenewInterval);
        await using var newcomer = Create(store, clock);
        await newcomer.StartAsync(CancellationToken.None);
        Assert.Equal(0, newcomer.Current.ValueOrThrow());
        Assert.Equal(1, source.Current.ValueOrThrow());
    }

    [Fact]
    public async Task StartAsync_Throws_WhenEveryWorkerIdIsLeased()
    {
        var clock = new FakeTimeProvider(_start);
        await using var source = Create(new SwitchableCacheStore(Store(clock)) { Refuse = true }, clock);

        var exception = await Assert.ThrowsAsync<FrameworkException>(() => source.StartAsync(CancellationToken.None));

        Assert.Equal(PersistenceErrors.WorkerIdUnavailable, exception.Code);
        Assert.False(source.Current.HasValue);
    }

    [Fact]
    public async Task BackgroundLoop_RenewsEveryThirdOfTimeToLive()
    {
        var clock = new FakeTimeProvider(_start);
        await using var source = Create(Store(clock), clock);
        await source.StartAsync(CancellationToken.None);
        var initial = source.ExpiresAt;

        clock.Advance(source.RenewInterval);

        // 循环在线程池上跑：等它完成这一轮续约
        Assert.True(SpinWait.SpinUntil(() => source.ExpiresAt > initial, TimeSpan.FromSeconds(10)), "The renewal loop did not renew the lease.");
        Assert.Equal(_start + source.RenewInterval + _timeToLive, source.ExpiresAt);
        Assert.Equal(0, source.Current.ValueOrThrow());
    }

    /// <summary>
    /// 以假时钟计时的内存缓存存储。
    /// </summary>
    private static InMemoryCacheStore Store(TimeProvider clock) => new(new InMemoryCachingOptions(), clock);

    /// <summary>
    /// 架在 <paramref name="store"/> 的分布式锁上的机器位来源。
    /// </summary>
    private static LeasedWorkerIdSource Create(ICacheStore store, TimeProvider clock) =>
        new(new StoreDistributedLock(store), new SnowflakeOptions { LeaseTimeToLive = _timeToLive }, clock, NullLoggerFactory.Instance);

    /// <summary>
    /// 机器位在存储里的锁条目键（<see cref="StoreDistributedLock"/> 的前缀 + 资源名）。
    /// </summary>
    private static CacheKey LockKey(int workerId) => new("lock:" + LeasedWorkerIdSource.ResourcePrefix + workerId);
}
