// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.InMemory.Tests;

/// <summary>
/// 进程内对象缓存的 <see cref="ICache.GetOrCreateAsync"/> 合并同键并发（ADR-0271）
/// </summary>
/// <remarks>
/// 只回源一次、失败与取消的归属、回源期间的写入与删除让它脱钩且一定不写回、不同类型不合并、真实线程下的并发与泄漏
/// </remarks>
public sealed class InMemoryObjectCacheLoadMergingTests
{
    /// <summary>
    /// 等一个本该很快结束的任务的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 条目的过期选项
    /// </summary>
    private static readonly CacheEntryOptions _entry = CacheEntryOptions.Expires(TimeSpan.FromMinutes(5));

    /// <summary>
    /// 假时钟：用例里不推进，条目不会过期
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// 被测缓存
    /// </summary>
    private readonly InMemoryObjectCache _cache;

    /// <summary>
    /// 创建夹具：容量足够大，用例里不会淘汰
    /// </summary>
    public InMemoryObjectCacheLoadMergingTests()
    {
        var options = new InMemoryCachingOptions { MaxEntries = 100_000, DefaultTimeToLive = TimeSpan.FromMinutes(5) };
        _cache = new InMemoryObjectCache(new InMemoryCacheStore(options, _clock), options, _clock);
    }

    [Fact]
    public async Task ConcurrentMisses_RunTheFactoryOnce_AndEveryCallGetsTheSameObject()
    {
        var key = new CacheKey("report:1");
        var gate = new TaskCompletionSource<object>();
        var factoryCalls = 0;
        var calls = Enumerable.Range(0, 50).Select(_ => _cache.GetOrCreateAsync(key, _ =>
        {
            factoryCalls++;
            return new ValueTask<object>(gate.Task);
        }, _entry, CancellationToken.None).AsTask()).ToList();
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, _cache.InFlightCount);
        Assert.Equal(0, _cache.Count);

        var value = new object();
        gate.SetResult(value);
        Assert.All(await Task.WhenAll(calls).WaitAsync(_patience), result => Assert.Same(value, result));
        Assert.Equal(1, factoryCalls);
        Assert.Equal(0, _cache.InFlightCount);
        Assert.Equal(1, _cache.Count);

        // 命中不进在途表，同步完成
        var hit = _cache.GetOrCreateAsync<object>(key, static _ => throw new InvalidOperationException("The factory should not be called again."), _entry, CancellationToken.None);
        Assert.True(hit.IsCompletedSuccessfully);
        Assert.Same(value, await hit);
    }

    [Fact]
    public async Task FactoryFailure_ReachesEveryWaiter_AndTheNextCallLoadsAgain()
    {
        var key = new CacheKey("report:1");
        var gate = new TaskCompletionSource<string>();
        var calls = Enumerable.Range(0, 5).Select(_ => _cache.GetOrCreateAsync(key, _ => new ValueTask<string>(gate.Task), _entry, CancellationToken.None).AsTask()).ToList();
        var failure = new InvalidOperationException("The source is down.");
        gate.SetException(failure);

        foreach (var call in calls)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => call.WaitAsync(_patience)));
        }

        // 失败不缓存
        Assert.Equal(0, _cache.Count);
        Assert.Equal(0, _cache.InFlightCount);
        Assert.Equal("recovered", await _cache.GetOrCreateAsync(key, static _ => ValueTask.FromResult("recovered"), _entry, CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_OfAWaiterEndsItsWait_OfTheLeaderHandsTheLoadOver()
    {
        var key = new CacheKey("report:1");
        using var leaderSource = new CancellationTokenSource();
        using var waiterSource = new CancellationTokenSource();
        var factoryToken = CancellationToken.None;
        var leader = _cache.GetOrCreateAsync(key, async token =>
        {
            factoryToken = token;
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return "from-the-leader";
        }, _entry, leaderSource.Token).AsTask();
        var cancelled = _cache.GetOrCreateAsync<string>(key, static _ => throw new InvalidOperationException("A cancelled waiter should not load."), _entry, waiterSource.Token).AsTask();
        var takeover = new TaskCompletionSource<string>();
        var takeoverCalls = 0;
        var waiters = Enumerable.Range(0, 3).Select(_ => _cache.GetOrCreateAsync(key, _ =>
        {
            Interlocked.Increment(ref takeoverCalls);
            return new ValueTask<string>(takeover.Task);
        }, _entry, CancellationToken.None).AsTask()).ToList();

        // 工厂收到的是领头调用的令牌；一个等待的调用取消只结束它自己
        Assert.Equal(leaderSource.Token, factoryToken);
        await waiterSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(_patience));
        Assert.False(factoryToken.IsCancellationRequested);
        Assert.False(leader.IsCompleted);

        // 领头的调用取消：工厂随它取消，等待的调用里一个接手、其余的等它
        await leaderSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leader.WaitAsync(_patience));
        await WaitUntilAsync(() => Volatile.Read(ref takeoverCalls) == 1);
        takeover.SetResult("from-a-waiter");
        Assert.All(await Task.WhenAll(waiters).WaitAsync(_patience), static value => Assert.Equal("from-a-waiter", value));
        Assert.Equal(1, takeoverCalls);
        Assert.Equal("from-a-waiter", (await _cache.GetAsync<string>(key, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(0, _cache.InFlightCount);
    }

    [Fact]
    public async Task RemovingTheKeyDuringTheLoad_DetachesIt_AndTheStaleResultIsNeverWritten()
    {
        var key = new CacheKey("price:1");
        var stale = new TaskCompletionSource<string>();
        var leader = _cache.GetOrCreateAsync(key, _ => new ValueTask<string>(stale.Task), _entry, CancellationToken.None).AsTask();
        var joinedBefore = _cache.GetOrCreateAsync<string>(key, static _ => throw new InvalidOperationException("A waiter should not load."), _entry, CancellationToken.None).AsTask();

        // 源改了、缓存删了：删除之后才到的调用不加入删除之前开始的回源
        await _cache.RemoveAsync(key, CancellationToken.None);
        Assert.Equal(0, _cache.InFlightCount);
        Assert.Equal("fresh", await _cache.GetOrCreateAsync(key, static _ => ValueTask.FromResult("fresh"), _entry, CancellationToken.None));

        // 删除之前就在等的调用照常拿到那次回源的结果；它不写回，缓存里仍是新值
        stale.SetResult("stale");
        Assert.Equal(["stale", "stale"], await Task.WhenAll(leader, joinedBefore).WaitAsync(_patience));
        Assert.Equal("fresh", (await _cache.GetAsync<string>(key, CancellationToken.None)).ValueOrThrow());
    }

    [Theory]
    [InlineData("remove-many")]
    [InlineData("remove-by-prefix")]
    [InlineData("set")]
    [InlineData("set-many")]
    public async Task EveryWriteOrRemovalOfTheKey_DetachesTheLoad(string change)
    {
        var key = new CacheKey("price:1");
        var stale = new TaskCompletionSource<string>();
        var leader = _cache.GetOrCreateAsync(key, _ => new ValueTask<string>(stale.Task), _entry, CancellationToken.None).AsTask();
        Assert.Equal(1, _cache.InFlightCount);

        string? expected = null;
        switch (change)
        {
            case "remove-many":
                await _cache.RemoveManyAsync([key, new CacheKey("price:2")], CancellationToken.None);
                break;
            case "remove-by-prefix":
                Assert.Equal(0, (await _cache.RemoveByPrefixAsync("price:", CancellationToken.None)).ValueOrThrow());
                break;
            case "set":
                expected = "written";
                await _cache.SetAsync(key, "written", _entry, CancellationToken.None);
                break;
            default:
                expected = "written";
                await _cache.SetManyAsync([new KeyValuePair<CacheKey, string>(key, "written")], _entry, CancellationToken.None);
                break;
        }

        Assert.Equal(0, _cache.InFlightCount);
        stale.SetResult("stale");
        Assert.Equal("stale", await leader.WaitAsync(_patience));

        // 回源的结果没有盖掉显式写入的值，也没有在删除之后把旧值放回来（合并之前是后写覆盖先写）
        var cached = await _cache.GetAsync<string>(key, CancellationToken.None);
        Assert.Equal(expected, cached.HasValue ? cached.ValueOrThrow() : null);
    }

    [Fact]
    public async Task DifferentResultTypes_ForTheSameKey_AreNotMerged()
    {
        var key = new CacheKey("mixed");
        var text = new TaskCompletionSource<string>();
        var asText = _cache.GetOrCreateAsync(key, _ => new ValueTask<string>(text.Task), _entry, CancellationToken.None).AsTask();

        // 同一个键被以别的类型读取：类型不匹配本来就算未命中，不等在途的那次，自己回源并写入
        Assert.Equal(42, await _cache.GetOrCreateAsync(key, static _ => ValueTask.FromResult(42), _entry, CancellationToken.None).AsTask().WaitAsync(_patience));
        Assert.Equal(42, (await _cache.GetAsync<int>(key, CancellationToken.None)).ValueOrThrow());
        Assert.False(asText.IsCompleted);

        // 两次回源各写各的，后写覆盖先写
        text.SetResult("text");
        Assert.Equal("text", await asText.WaitAsync(_patience));
        Assert.Equal("text", (await _cache.GetAsync<string>(key, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(0, _cache.InFlightCount);
    }

    [Fact]
    public async Task InvalidOptions_FailBeforeTheFactoryRuns_OnAMiss()
    {
        var key = new CacheKey("report:1");
        var both = new CacheEntryOptions { TimeToLive = TimeSpan.FromSeconds(1), SlidingExpiration = TimeSpan.FromSeconds(1) };
        var factoryCalls = 0;
        await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.GetOrCreateAsync(key, _ =>
        {
            factoryCalls++;
            return ValueTask.FromResult("unused");
        }, both, CancellationToken.None));
        Assert.Equal(0, factoryCalls);
        Assert.Equal(0, _cache.InFlightCount);
    }

    [Fact]
    public async Task ParallelCallers_OnManyKeys_RunEachFactoryOnce_AndLeaveNothingInFlight()
    {
        const int Rounds = 20;
        const int Keys = 8;
        const int CallersPerKey = 32;
        var factoryCalls = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        for (var round = 0; round < Rounds; round++)
        {
            // 异步的起跑信号：不让几百个任务各占一个线程池线程干等
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = new List<Task<object>>(Keys * CallersPerKey);
            for (var caller = 0; caller < CallersPerKey; caller++)
            {
                for (var index = 0; index < Keys; index++)
                {
                    var key = new CacheKey($"round-{round}:key-{index}");
                    calls.Add(Task.Run(async () =>
                    {
                        await start.Task;
                        return await _cache.GetOrCreateAsync(key, async token =>
                        {
                            factoryCalls.AddOrUpdate(key.Value, 1, static (_, count) => count + 1);
                            // 让出线程再回来：回源还在途中时别的调用有机会撞上它
                            await Task.Yield();
                            await Task.Delay(1, token);
                            return new object();
                        }, _entry, CancellationToken.None);
                    }));
                }
            }

            start.SetResult();
            var values = await Task.WhenAll(calls).WaitAsync(_patience);
            for (var index = 0; index < Keys; index++)
            {
                Assert.Equal(1, factoryCalls[$"round-{round}:key-{index}"]);
                Assert.Single(values.Where((_, position) => position % Keys == index).Distinct());
            }

            Assert.Equal(0, _cache.InFlightCount);
        }
    }

    [Fact]
    public async Task RemovalRacingTheWriteBack_NeverLeavesTheStaleValueBehind()
    {
        // 删除发生在回源开始之后、与写回同时：无论谁先谁后，两边都结束时缓存里不能留着那次回源的值
        for (var iteration = 0; iteration < 2000; iteration++)
        {
            var key = new CacheKey($"race:{iteration}");
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var load = Task.Run(async () => await _cache.GetOrCreateAsync(key, async _ =>
            {
                started.TrySetResult();
                await Task.Yield();
                return "stale";
            }, _entry, CancellationToken.None));
            await started.Task.WaitAsync(_patience);
            await _cache.RemoveAsync(key, CancellationToken.None);

            Assert.Equal("stale", await load.WaitAsync(_patience));
            Assert.False((await _cache.GetAsync<string>(key, CancellationToken.None)).HasValue, $"Iteration {iteration} left the stale value in the cache.");
        }

        Assert.Equal(0, _cache.InFlightCount);
        Assert.Equal(0, _cache.Count);
    }

    /// <summary>
    /// 轮询到条件成立，超时即失败
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + _patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            await Task.Delay(5);
        }
    }
}
