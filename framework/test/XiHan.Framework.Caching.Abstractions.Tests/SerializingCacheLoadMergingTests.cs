// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Serialization;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 序列化缓存的 <see cref="ICache.GetOrCreateAsync"/> 合并同键并发（ADR-0271）
/// </summary>
/// <remarks>
/// 存储替身的读写都同步完成，调用在测试线程上一直跑到工厂挂起为止：先起的是领头，后起的排在它后面，次序是确定的
/// </remarks>
public sealed class SerializingCacheLoadMergingTests
{
    /// <summary>
    /// 等一个本该很快结束的任务的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 被测缓存用的键
    /// </summary>
    private static readonly CacheKey _key = new("profile:1");

    /// <summary>
    /// 字节存储替身
    /// </summary>
    private readonly FakeCacheStore _store = new();

    /// <summary>
    /// 被测缓存
    /// </summary>
    private readonly SerializingCache _cache;

    /// <summary>
    /// 工厂被调用的次数
    /// </summary>
    private int _factoryCalls;

    /// <summary>
    /// 创建
    /// </summary>
    public SerializingCacheLoadMergingTests()
    {
        _cache = new SerializingCache(_store, JsonCacheSerializer.CreateReflectionBased());
    }

    [Fact]
    public async Task ConcurrentMisses_RunTheFactoryOnce_AndWriteOnce()
    {
        var gate = new TaskCompletionSource<string>();
        var calls = Enumerable.Range(0, 20).Select(_ => GetOrCreateAsync(gate.Task, CancellationToken.None)).ToList();
        Assert.Equal(1, _factoryCalls);
        Assert.Equal(1, _cache.InFlightCount);

        gate.SetResult("ada");
        Assert.All(await Task.WhenAll(calls).WaitAsync(_patience), static value => Assert.Equal("ada", value));
        Assert.Equal(1, _factoryCalls);
        Assert.Equal(0, _cache.InFlightCount);

        // 每个调用先各读一次（命中的快路径），领头的调用登记之后再读一次，写回只有一次
        Assert.Equal(21, _store.Operations.Count(static op => op == "get:profile:1"));
        Assert.Equal(1, _store.Operations.Count(static op => op == "set:profile:1"));

        // 之后命中，不进在途表
        _store.Operations.Clear();
        Assert.Equal("ada", await _cache.GetOrCreateAsync<string>(_key, static _ => throw new InvalidOperationException("The factory should not be called again."), CacheEntryOptions.Default, CancellationToken.None));
        Assert.Equal(["get:profile:1"], _store.Operations);
    }

    [Fact]
    public async Task FactoryFailure_ReachesEveryWaiter_AndNothingIsCached()
    {
        var gate = new TaskCompletionSource<string>();
        var calls = Enumerable.Range(0, 5).Select(_ => GetOrCreateAsync(gate.Task, CancellationToken.None)).ToList();
        var failure = new InvalidOperationException("The source is down.");
        gate.SetException(failure);

        foreach (var call in calls)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => call.WaitAsync(_patience)));
        }

        Assert.Equal(0, _store.Count);
        Assert.Equal(0, _cache.InFlightCount);

        // 下一次调用重新回源
        Assert.Equal("ada", await GetOrCreateAsync(Task.FromResult("ada"), CancellationToken.None));
        Assert.Equal(2, _factoryCalls);
    }

    [Fact]
    public async Task WaiterCancellation_LeavesTheFactoryRunning_WhileLeaderCancellationHandsOver()
    {
        var first = new TaskCompletionSource<string>();
        using var leaderSource = new CancellationTokenSource();
        using var waiterSource = new CancellationTokenSource();
        var leader = _cache.GetOrCreateAsync(_key, async token =>
        {
            _factoryCalls++;
            return await first.Task.WaitAsync(token);
        }, CacheEntryOptions.Default, leaderSource.Token).AsTask();
        var cancelled = GetOrCreateAsync(Task.FromResult("unused"), waiterSource.Token);
        var second = new TaskCompletionSource<string>();
        var patient = GetOrCreateAsync(second.Task, CancellationToken.None);

        // 一个等待的调用取消：只有它自己结束
        await waiterSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(_patience));
        Assert.False(leader.IsCompleted);
        Assert.False(patient.IsCompleted);
        Assert.Equal(1, _factoryCalls);

        // 领头的调用取消：它的工厂随它取消，剩下的那个等待者接手、执行自己的工厂
        await leaderSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leader.WaitAsync(_patience));
        await WaitUntilAsync(() => Volatile.Read(ref _factoryCalls) == 2);
        second.SetResult("from-the-waiter");
        Assert.Equal("from-the-waiter", await patient.WaitAsync(_patience));
        Assert.Equal("from-the-waiter", (await _cache.GetAsync<string>(_key, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(0, _cache.InFlightCount);
    }

    [Fact]
    public async Task RemovingTheKeyDuringTheLoad_KeepsTheStaleResultOutOfTheStore()
    {
        var stale = new TaskCompletionSource<string>();
        var leader = GetOrCreateAsync(stale.Task, CancellationToken.None);
        var joinedBefore = GetOrCreateAsync(Task.FromResult("unused"), CancellationToken.None);

        // 源改了、缓存删了：在途的回源读到的可能是改之前的数据
        Assert.False(await _cache.RemoveAsync(_key, CancellationToken.None));
        Assert.Equal(0, _cache.InFlightCount);

        // 删除之后才到的调用不加入它，自己回源并写回
        Assert.Equal("fresh", await GetOrCreateAsync(Task.FromResult("fresh"), CancellationToken.None));
        Assert.Equal(2, _factoryCalls);

        // 删除之前就在等的调用照常拿到那次回源的结果，但它不再写回
        stale.SetResult("stale");
        Assert.Equal(["stale", "stale"], await Task.WhenAll(leader, joinedBefore).WaitAsync(_patience));
        Assert.Equal("fresh", (await _cache.GetAsync<string>(_key, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(1, _store.Operations.Count(static op => op == "set:profile:1"));

        // 批量删除同样让在途的回源脱钩
        await _cache.RemoveManyAsync([_key], CancellationToken.None);
        var again = new TaskCompletionSource<string>();
        var second = GetOrCreateAsync(again.Task, CancellationToken.None);
        await _cache.RemoveManyAsync([_key], CancellationToken.None);
        again.SetResult("stale-again");
        Assert.Equal("stale-again", await second.WaitAsync(_patience));
        Assert.False((await _cache.GetAsync<string>(_key, CancellationToken.None)).HasValue);
    }

    [Fact]
    public async Task WritingTheKeyDuringTheLoad_KeepsTheWrittenValue()
    {
        var stale = new TaskCompletionSource<string>();
        var leader = GetOrCreateAsync(stale.Task, CancellationToken.None);

        // 回源期间显式写入：之后的调用直接命中写入的值
        await _cache.SetAsync(_key, "written", CacheEntryOptions.Default, CancellationToken.None);
        Assert.Equal("written", await GetOrCreateAsync(Task.FromResult("unused"), CancellationToken.None));
        Assert.Equal(1, _factoryCalls);

        // 回源的结果交给它自己的调用方，不盖掉写入的值（合并之前是后写覆盖先写）
        stale.SetResult("stale");
        Assert.Equal("stale", await leader.WaitAsync(_patience));
        Assert.Equal("written", (await _cache.GetAsync<string>(_key, CancellationToken.None)).ValueOrThrow());

        // 批量写入同样
        await _cache.RemoveAsync(_key, CancellationToken.None);
        var again = new TaskCompletionSource<string>();
        var second = GetOrCreateAsync(again.Task, CancellationToken.None);
        await _cache.SetManyAsync([new KeyValuePair<CacheKey, string>(_key, "batch")], CacheEntryOptions.Default, CancellationToken.None);
        again.SetResult("stale-again");
        Assert.Equal("stale-again", await second.WaitAsync(_patience));
        Assert.Equal("batch", (await _cache.GetAsync<string>(_key, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task StoreFailures_AreThrownAsBefore_AndLeaveNothingInFlight()
    {
        // 读不到：照常抛出，不改为直接回源
        _store.FailingReads = true;
        var read = await Assert.ThrowsAsync<FrameworkException>(async () => await GetOrCreateAsync(Task.FromResult("unused"), CancellationToken.None));
        Assert.Equal(CachingErrors.StoreUnavailable, read.Code);
        Assert.Equal(0, _factoryCalls);
        Assert.Equal(0, _cache.InFlightCount);

        // 写不回去：工厂已经执行，领头的与等待的调用收到同一个异常
        _store.FailingReads = false;
        _store.FailingWrites = true;
        var gate = new TaskCompletionSource<string>();
        var calls = Enumerable.Range(0, 3).Select(_ => GetOrCreateAsync(gate.Task, CancellationToken.None)).ToList();
        gate.SetResult("ada");
        var first = await Assert.ThrowsAsync<FrameworkException>(() => calls[0].WaitAsync(_patience));
        Assert.Equal(CachingErrors.StoreUnavailable, first.Code);
        Assert.Same(first, await Assert.ThrowsAsync<FrameworkException>(() => calls[1].WaitAsync(_patience)));
        Assert.Same(first, await Assert.ThrowsAsync<FrameworkException>(() => calls[2].WaitAsync(_patience)));
        Assert.Equal(1, _factoryCalls);
        Assert.Equal(0, _cache.InFlightCount);

        // 后端恢复后照常回源
        _store.FailingWrites = false;
        Assert.Equal("ada", await GetOrCreateAsync(Task.FromResult("ada"), CancellationToken.None));
        Assert.Equal(2, _factoryCalls);
    }

    [Fact]
    public async Task InvalidOptionsAndNullResults_FailWithoutLeavingAnythingInFlight()
    {
        // 选项不合法：未命中时在回源之前就抛出
        var both = new CacheEntryOptions { TimeToLive = TimeSpan.FromSeconds(1), SlidingExpiration = TimeSpan.FromSeconds(1) };
        await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.GetOrCreateAsync(_key, _ =>
        {
            _factoryCalls++;
            return ValueTask.FromResult("unused");
        }, both, CancellationToken.None));
        Assert.Equal(0, _factoryCalls);

        // 工厂交出空值：与 SetAsync 一样不接受
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _cache.GetOrCreateAsync<string>(_key, static _ => ValueTask.FromResult<string>(null!), CacheEntryOptions.Default, CancellationToken.None));
        Assert.Equal(0, _store.Count);
        Assert.Equal(0, _cache.InFlightCount);
    }

    /// <summary>
    /// 以「等这个任务」为工厂取或建，并数工厂被调用的次数
    /// </summary>
    private Task<string> GetOrCreateAsync(Task<string> source, CancellationToken cancellationToken)
        => _cache.GetOrCreateAsync(_key, _ =>
        {
            Interlocked.Increment(ref _factoryCalls);
            return new ValueTask<string>(source);
        }, CacheEntryOptions.Default, cancellationToken).AsTask();

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
