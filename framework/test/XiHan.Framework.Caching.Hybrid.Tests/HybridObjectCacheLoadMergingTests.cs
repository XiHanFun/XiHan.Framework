// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.Hybrid.Tests;

/// <summary>
/// 两级缓存的 <see cref="ICache.GetOrCreateAsync"/> 合并同键并发（ADR-0271）
/// </summary>
/// <remarks>
/// L1 未命中的并发调用只读一次 L2、两级都未命中只执行一次工厂；本实例的写入、删除与 L2 存储的变更通知让在途的回源脱钩。L2 替身的读写同步完成（除非用 <see cref="RecordingSharedCache.BeforeRead"/> 挡住），调用的先后是确定的
/// </remarks>
public sealed class HybridObjectCacheLoadMergingTests : IDisposable
{
    /// <summary>
    /// 等一个本该很快结束的任务的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 被测缓存用的键
    /// </summary>
    private static readonly CacheKey _key = new("user:1");

    /// <summary>
    /// 固定起点的可控时钟
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// 会发变更通知的 L2 存储
    /// </summary>
    private readonly FeedingStore _feed;

    /// <summary>
    /// L2 替身
    /// </summary>
    private readonly RecordingSharedCache _shared;

    /// <summary>
    /// 被测缓存
    /// </summary>
    private readonly HybridObjectCache _cache;

    /// <summary>
    /// 工厂被调用的次数
    /// </summary>
    private int _factoryCalls;

    /// <summary>
    /// 创建夹具
    /// </summary>
    public HybridObjectCacheLoadMergingTests()
    {
        _feed = new FeedingStore(_clock);
        _shared = new RecordingSharedCache(_clock, _feed);
        _cache = new HybridObjectCache(_shared, new HybridCachingOptions { LocalTimeToLive = TimeSpan.FromMinutes(5) }, _clock);
    }

    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task ConcurrentMisses_ReadTheSharedCacheOnce_RunTheFactoryOnce_AndWriteOnce()
    {
        var gate = new TaskCompletionSource<string>();
        var calls = Enumerable.Range(0, 20).Select(_ => GetOrCreateAsync(gate.Task)).ToList();

        // 二十个调用都没在 L1 命中：只有领头的去读了 L2、执行了工厂
        Assert.Equal(["get:user:1"], _shared.Operations);
        Assert.Equal(1, _factoryCalls);
        Assert.Equal(1, _cache.InFlightCount);

        gate.SetResult("ada");
        Assert.All(await Task.WhenAll(calls).WaitAsync(_patience), static value => Assert.Equal("ada", value));
        Assert.Equal(["get:user:1", "set:user:1"], _shared.Operations);
        Assert.Equal(1, _factoryCalls);
        Assert.Equal(0, _cache.InFlightCount);

        // 结果回填了 L1：之后的调用不出进程
        Assert.Equal("ada", await GetOrCreateAsync(Task.FromResult("unused")));
        Assert.Equal(["get:user:1", "set:user:1"], _shared.Operations);
    }

    [Fact]
    public async Task ConcurrentMisses_OnASharedHit_ShareOneRead_WithoutRunningTheFactory()
    {
        await _shared.SetAsync(_key, "shared", CacheEntryOptions.Default, CancellationToken.None);
        _shared.Operations.Clear();

        // 读 L2 停在途中：L1 过期后同时到来的调用只读一次 L2
        var reading = new TaskCompletionSource();
        _shared.BeforeRead = reading.Task;
        var calls = Enumerable.Range(0, 10).Select(_ => GetOrCreateAsync(Task.FromResult("unused"))).ToList();
        Assert.Equal(["get:user:1"], _shared.Operations);
        Assert.Equal(1, _cache.InFlightCount);

        _shared.BeforeRead = null;
        reading.SetResult();
        Assert.All(await Task.WhenAll(calls).WaitAsync(_patience), static value => Assert.Equal("shared", value));
        Assert.Equal(["get:user:1"], _shared.Operations);
        Assert.Equal(0, _factoryCalls);
        Assert.Equal(0, _cache.InFlightCount);
        Assert.Equal(1, _cache.LocalCount);
    }

    [Fact]
    public async Task ChangeNotificationDuringTheLoad_DetachesIt_SoLaterCallsReadTheSharedCacheAgain()
    {
        var stale = new TaskCompletionSource<string>();
        var leader = GetOrCreateAsync(stale.Task);
        var joinedBefore = GetOrCreateAsync(Task.FromResult("unused"));
        Assert.Equal(1, _cache.InFlightCount);

        // 别的实例写了这个键，变更通知到达：在途的回源读到的可能是写之前的数据
        await _shared.SetAsync(_key, "from-another-instance", CacheEntryOptions.Default, CancellationToken.None);
        _feed.Raise(CacheChange.ForKeys([_key]));
        Assert.Equal(0, _cache.InFlightCount);
        _shared.Operations.Clear();

        // 通知之后才到的调用不加入它：重新读 L2，拿到新值，不执行工厂
        Assert.Equal("from-another-instance", await GetOrCreateAsync(Task.FromResult("unused")));
        Assert.Equal(["get:user:1"], _shared.Operations);
        Assert.Equal(1, _factoryCalls);

        // 通知之前就在等的调用照常拿到那次回源的结果；它不写进 L2，也不回填 L1
        stale.SetResult("stale");
        Assert.Equal(["stale", "stale"], await Task.WhenAll(leader, joinedBefore).WaitAsync(_patience));
        Assert.Equal(["get:user:1"], _shared.Operations);
        Assert.Equal("from-another-instance", (await _cache.GetAsync<string>(_key, CancellationToken.None)).ValueOrThrow());
        Assert.Equal("from-another-instance", (await _shared.GetAsync<string>(_key, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task PrefixNotificationDuringTheLoad_DetachesTheLoadsUnderThePrefix()
    {
        var user = new TaskCompletionSource<string>();
        var role = new TaskCompletionSource<string>();
        var roleKey = new CacheKey("role:1");
        var userLoad = GetOrCreateAsync(user.Task);
        var roleLoad = _cache.GetOrCreateAsync(roleKey, _ => new ValueTask<string>(role.Task), CacheEntryOptions.Default, CancellationToken.None).AsTask();
        Assert.Equal(2, _cache.InFlightCount);

        // 别的实例按前缀删除：只有前缀下的回源脱钩
        _feed.Raise(CacheChange.ForPrefix("user:"));
        Assert.Equal(1, _cache.InFlightCount);

        user.SetResult("stale-user");
        role.SetResult("role");
        Assert.Equal(["stale-user", "role"], await Task.WhenAll(userLoad, roleLoad).WaitAsync(_patience));

        // 脱钩的没有写进 L2，另一个照常写入
        Assert.DoesNotContain("set:user:1", _shared.Operations);
        Assert.Contains("set:role:1", _shared.Operations);
        Assert.Equal(0, _cache.InFlightCount);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("remove-many")]
    [InlineData("set")]
    [InlineData("set-many")]
    public async Task OwnWriteOrRemovalDuringTheLoad_DetachesIt(string change)
    {
        var stale = new TaskCompletionSource<string>();
        var leader = GetOrCreateAsync(stale.Task);
        Assert.Equal(1, _cache.InFlightCount);

        string? expected = null;
        switch (change)
        {
            case "remove":
                await _cache.RemoveAsync(_key, CancellationToken.None);
                break;
            case "remove-many":
                await _cache.RemoveManyAsync([_key], CancellationToken.None);
                break;
            case "set":
                expected = "written";
                await _cache.SetAsync(_key, "written", CacheEntryOptions.Default, CancellationToken.None);
                break;
            default:
                expected = "written";
                await _cache.SetManyAsync([new KeyValuePair<CacheKey, string>(_key, "written")], CacheEntryOptions.Default, CancellationToken.None);
                break;
        }

        Assert.Equal(0, _cache.InFlightCount);
        stale.SetResult("stale");
        Assert.Equal("stale", await leader.WaitAsync(_patience));

        // 回源的结果没有写进 L2：显式写入的值还在，删除之后也没有把旧值放回来
        var shared = await _shared.GetAsync<string>(_key, CancellationToken.None);
        Assert.Equal(expected, shared.HasValue ? shared.ValueOrThrow() : null);
        var local = await _cache.GetAsync<string>(_key, CancellationToken.None);
        Assert.Equal(expected, local.HasValue ? local.ValueOrThrow() : null);
    }

    [Fact]
    public async Task FactoryFailure_ReachesEveryWaiter_AndNothingIsWritten()
    {
        var gate = new TaskCompletionSource<string>();
        var calls = Enumerable.Range(0, 5).Select(_ => GetOrCreateAsync(gate.Task)).ToList();
        var failure = new InvalidOperationException("The source is down.");
        gate.SetException(failure);

        foreach (var call in calls)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => call.WaitAsync(_patience)));
        }

        Assert.Equal(["get:user:1"], _shared.Operations);
        Assert.Equal(0, _cache.LocalCount);
        Assert.Equal(0, _cache.InFlightCount);

        // 下一次调用重新读 L2、重新回源
        Assert.Equal("recovered", await GetOrCreateAsync(Task.FromResult("recovered")));
        Assert.Equal(2, _factoryCalls);
    }

    /// <summary>
    /// 以「等这个任务」为工厂取或建，并数工厂被调用的次数
    /// </summary>
    private Task<string> GetOrCreateAsync(Task<string> source)
        => _cache.GetOrCreateAsync(_key, _ =>
        {
            Interlocked.Increment(ref _factoryCalls);
            return new ValueTask<string>(source);
        }, CacheEntryOptions.Default, CancellationToken.None).AsTask();
}
