// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 在途表（ADR-0271）
/// </summary>
/// <remarks>
/// 同键并发只回源一次、失败与取消的归属、领头取消后的交接、被取代的回源脱钩且不写回、不合并的两种情况、表里不留条目。回源函数照缓存实现的写法：先再读一次缓存，未命中才查源并写回
/// </remarks>
public sealed class InFlightLoadTableTests
{
    /// <summary>
    /// 等一个本该很快结束的任务的上限：出错时用例失败而不是挂住
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 被测的在途表
    /// </summary>
    private readonly InFlightLoadTable _table = new();

    /// <summary>
    /// 替身缓存：键 → 值
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// 每个键查源的次数
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _sourceCalls = new(StringComparer.Ordinal);

    [Fact]
    public async Task ConcurrentCalls_ForTheSameKey_LoadOnce_AndShareTheResult()
    {
        var key = new CacheKey("k");
        var gate = new TaskCompletionSource<string>();
        var calls = Enumerable.Range(0, 50).Select(_ => GetOrLoadAsync(key, _ => new ValueTask<string>(gate.Task), CancellationToken.None).AsTask()).ToList();

        // 第一个调用在查源，其余四十九个在等它
        Assert.Equal(1, _sourceCalls["k"]);
        Assert.Equal(1, _table.Count);
        Assert.All(calls, static call => Assert.False(call.IsCompleted));

        gate.SetResult("value");
        Assert.All(await Task.WhenAll(calls).WaitAsync(_patience), static value => Assert.Equal("value", value));
        Assert.Equal(1, _sourceCalls["k"]);
        Assert.Equal("value", _cache["k"]);
        // 回源结束即移除
        Assert.Equal(0, _table.Count);

        // 之后命中缓存，不再查源
        Assert.Equal("value", await GetOrLoadAsync(key, static _ => throw new InvalidOperationException("The source should not be read again."), CancellationToken.None));
    }

    [Fact]
    public async Task DifferentKeys_LoadIndependently()
    {
        var first = new TaskCompletionSource<string>();
        var second = new TaskCompletionSource<string>();
        var a = GetOrLoadAsync(new CacheKey("a"), _ => new ValueTask<string>(first.Task), CancellationToken.None).AsTask();
        var b = GetOrLoadAsync(new CacheKey("b"), _ => new ValueTask<string>(second.Task), CancellationToken.None).AsTask();
        Assert.Equal(2, _table.Count);

        // 后登记的先结束：互不等待
        second.SetResult("2");
        Assert.Equal("2", await b.WaitAsync(_patience));
        Assert.False(a.IsCompleted);
        Assert.Equal(1, _table.Count);
        first.SetResult("1");
        Assert.Equal("1", await a.WaitAsync(_patience));
        Assert.Equal(0, _table.Count);
    }

    [Fact]
    public async Task LoadFailure_ReachesEveryWaiterAsTheSameException_AndIsNotRemembered()
    {
        var key = new CacheKey("k");
        var gate = new TaskCompletionSource<string>();
        var calls = Enumerable.Range(0, 10).Select(_ => GetOrLoadAsync(key, _ => new ValueTask<string>(gate.Task), CancellationToken.None).AsTask()).ToList();

        var failure = new InvalidOperationException("The source is down.");
        gate.SetException(failure);

        // 领头的与等待的收到的是同一个异常实例
        foreach (var call in calls)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => call.WaitAsync(_patience)));
        }

        Assert.Equal(1, _sourceCalls["k"]);
        Assert.Empty(_cache);
        Assert.Equal(0, _table.Count);

        // 失败不留下任何东西：下一次调用重新查源
        Assert.Equal("recovered", await GetOrLoadAsync(key, static _ => ValueTask.FromResult("recovered"), CancellationToken.None));
        Assert.Equal(2, _sourceCalls["k"]);
    }

    [Fact]
    public async Task WaiterCancellation_EndsOnlyItsOwnWait()
    {
        var key = new CacheKey("k");
        var gate = new TaskCompletionSource<string>();
        var seen = CancellationToken.None;
        using var leaderSource = new CancellationTokenSource();
        using var waiterSource = new CancellationTokenSource();
        var leader = GetOrLoadAsync(
            key,
            token =>
            {
                seen = token;
                return new ValueTask<string>(gate.Task);
            },
            leaderSource.Token).AsTask();
        var cancelled = GetOrLoadAsync(key, static _ => throw new InvalidOperationException("A waiter should not read the source."), waiterSource.Token).AsTask();
        var patient = GetOrLoadAsync(key, static _ => throw new InvalidOperationException("A waiter should not read the source."), CancellationToken.None).AsTask();

        // 回源函数拿到的是领头调用的令牌，不是哪个等待者的
        Assert.Equal(leaderSource.Token, seen);

        await waiterSource.CancelAsync();
        Assert.Same(cancelled, await Task.WhenAny(cancelled, Task.Delay(_patience)));
        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(waiterSource.Token, thrown.CancellationToken);

        // 别人的等待与回源都不受影响
        Assert.False(seen.IsCancellationRequested);
        Assert.False(leader.IsCompleted);
        Assert.False(patient.IsCompleted);
        Assert.Equal(1, _table.Count);

        gate.SetResult("value");
        Assert.Equal(["value", "value"], await Task.WhenAll(leader, patient).WaitAsync(_patience));
        Assert.Equal(1, _sourceCalls["k"]);
        Assert.Equal(0, _table.Count);
    }

    [Fact]
    public async Task LeaderCancellation_CancelsItsLoad_AndAWaiterTakesOverWithItsOwn()
    {
        var key = new CacheKey("k");
        using var leaderSource = new CancellationTokenSource();
        var leader = GetOrLoadAsync(key, static async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return "from-the-leader";
        }, leaderSource.Token).AsTask();
        var takeover = new TaskCompletionSource<string>();
        var waiters = Enumerable.Range(0, 5).Select(_ => GetOrLoadAsync(key, _ => new ValueTask<string>(takeover.Task), CancellationToken.None).AsTask()).ToList();
        Assert.Equal(1, _sourceCalls["k"]);

        // 领头的调用取消：它的回源随它取消，它自己收到取消
        await leaderSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leader.WaitAsync(_patience));

        // 等待的调用不收到取消：其中一个接手、执行它自己的回源函数，其余的等它
        await WaitUntilAsync(() => _sourceCalls["k"] == 2);
        Assert.All(waiters, static waiter => Assert.False(waiter.IsCompleted));
        Assert.Equal(1, _table.Count);

        takeover.SetResult("from-a-waiter");
        Assert.All(await Task.WhenAll(waiters).WaitAsync(_patience), static value => Assert.Equal("from-a-waiter", value));
        Assert.Equal(2, _sourceCalls["k"]);
        Assert.Equal("from-a-waiter", _cache["k"]);
        Assert.Equal(0, _table.Count);
    }

    [Fact]
    public async Task LeaderFailingAfterItsOwnCancellation_DoesNotFailTheWaiters()
    {
        var key = new CacheKey("k");
        using var leaderSource = new CancellationTokenSource();
        var leaderGate = new TaskCompletionSource<string>();
        var leader = GetOrLoadAsync(key, _ => new ValueTask<string>(leaderGate.Task), leaderSource.Token).AsTask();
        var waiter = GetOrLoadAsync(key, static _ => ValueTask.FromResult("from-the-waiter"), CancellationToken.None).AsTask();

        // 驱动把取消报成别的异常：领头的令牌已经取消，这个失败只算它自己的
        await leaderSource.CancelAsync();
        var failure = new InvalidOperationException("The command was aborted.");
        leaderGate.SetException(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => leader.WaitAsync(_patience)));
        Assert.Equal("from-the-waiter", await waiter.WaitAsync(_patience));
        Assert.Equal(2, _sourceCalls["k"]);
        Assert.Equal(0, _table.Count);
    }

    [Fact]
    public async Task EveryCallerCancelling_CancelsTheLoad_AndLeavesTheTableEmpty()
    {
        var key = new CacheKey("k");
        var loadCancelled = new TaskCompletionSource();
        using var leaderSource = new CancellationTokenSource();
        using var waiterSource = new CancellationTokenSource();
        var leader = GetOrLoadAsync(key, async token =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                loadCancelled.TrySetResult();
            }

            return "never";
        }, leaderSource.Token).AsTask();
        var waiters = Enumerable.Range(0, 5).Select(_ => GetOrLoadAsync(key, static _ => throw new InvalidOperationException("A cancelled waiter should not read the source."), waiterSource.Token).AsTask()).ToList();

        // 等待的调用先全部取消：领头的还在，回源照常进行
        await waiterSource.CancelAsync();
        foreach (var waiter in waiters)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter.WaitAsync(_patience));
        }

        Assert.False(loadCancelled.Task.IsCompleted);
        Assert.Equal(1, _table.Count);

        // 领头的也取消：回源取消，表里不留条目，没有人接手
        await leaderSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leader.WaitAsync(_patience));
        await loadCancelled.Task.WaitAsync(_patience);
        Assert.Equal(0, _table.Count);
        Assert.Equal(1, _sourceCalls["k"]);
        Assert.Empty(_cache);

        // 已经取消的调用既不回源也不排队
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await GetOrLoadAsync(key, static _ => throw new InvalidOperationException("A cancelled call should not read the source."), waiterSource.Token));
        Assert.Equal(1, _sourceCalls["k"]);

        // 下一次调用重新回源
        Assert.Equal("fresh", await GetOrLoadAsync(key, static _ => ValueTask.FromResult("fresh"), CancellationToken.None));
        Assert.Equal(2, _sourceCalls["k"]);
    }

    [Fact]
    public async Task Supersede_DetachesTheLoad_SoLaterCallsStartAnew_AndTheOldResultIsNotWritten()
    {
        var key = new CacheKey("k");
        var stale = new TaskCompletionSource<string>();
        InFlightLoad? handle = null;
        var leader = _table.RunAsync(key, async (load, _) =>
        {
            handle = load;
            var value = await stale.Task;
            Assert.False(load.TryWrite(value, written => _cache["k"] = written));
            return value;
        }, CancellationToken.None).AsTask();
        var joinedBefore = _table.RunAsync<string>(key, static (_, _) => throw new InvalidOperationException("A waiter should not load."), CancellationToken.None).AsTask();
        Assert.NotNull(handle);
        Assert.False(handle.IsSuperseded);

        // 这个键被删除（或写入）了：在途的回源与它脱钩，表里不再有它
        _table.Supersede(key);
        Assert.True(handle.IsSuperseded);
        Assert.Equal(0, _table.Count);

        // 之后到达的调用不加入它，另起一次回源并写回
        Assert.Equal("fresh", await GetOrLoadAsync(key, static _ => ValueTask.FromResult("fresh"), CancellationToken.None));
        Assert.Equal("fresh", _cache["k"]);

        // 脱钩的回源照常结束：已经在等的调用拿到它的结果，但结果没有盖掉后来写下的值
        stale.SetResult("stale");
        Assert.Equal(["stale", "stale"], await Task.WhenAll(leader, joinedBefore).WaitAsync(_patience));
        Assert.Equal("fresh", _cache["k"]);
        Assert.Equal(0, _table.Count);

        // 没有在途的回源时什么也不做
        _table.Supersede(key);
        _table.Supersede([]);
        _table.SupersedePrefix("k");
    }

    [Fact]
    public async Task Supersede_ByKeysAndByPrefix_DetachesExactlyThoseLoads()
    {
        var gate = new TaskCompletionSource<string>();
        var handles = new ConcurrentDictionary<string, InFlightLoad>(StringComparer.Ordinal);
        Task<string> Start(string name) => _table.RunAsync(new CacheKey(name), async (load, _) =>
        {
            handles[name] = load;
            return await gate.Task;
        }, CancellationToken.None).AsTask();
        string[] names = ["user:1", "user:2", "role:1", "role:2", "other"];
        var loads = names.Select(Start).ToList();
        Assert.Equal(5, _table.Count);

        // 按前缀：序数比较，只动前缀下的
        _table.SupersedePrefix("user:");
        Assert.Equal(3, _table.Count);
        Assert.Equal(["user:1", "user:2"], handles.Where(static pair => pair.Value.IsSuperseded).Select(static pair => pair.Key).Order(StringComparer.Ordinal));

        // 按键集合：不在途的与重复的键没有影响
        _table.Supersede([new CacheKey("role:1"), new CacheKey("missing"), new CacheKey("role:1")]);
        Assert.Equal(2, _table.Count);
        Assert.True(handles["role:1"].IsSuperseded);
        Assert.False(handles["role:2"].IsSuperseded);

        // 空前缀是全部
        _table.SupersedePrefix(string.Empty);
        Assert.Equal(0, _table.Count);
        Assert.All(handles.Values, static handle => Assert.True(handle.IsSuperseded));

        gate.SetResult("done");
        Assert.All(await Task.WhenAll(loads).WaitAsync(_patience), static value => Assert.Equal("done", value));
    }

    [Fact]
    public async Task Supersede_WaitsForAWriteInProgress_AndBlocksLaterOnes()
    {
        var key = new CacheKey("k");
        using var writing = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        var gate = new TaskCompletionSource<InFlightLoad>(TaskCreationOptions.RunContinuationsAsynchronously);
        var leader = _table.RunAsync(key, async (load, _) =>
        {
            gate.SetResult(load);
            await Task.Yield();
            return "value";
        }, CancellationToken.None).AsTask();
        var handle = await gate.Task.WaitAsync(_patience);
        Assert.Equal("value", await leader.WaitAsync(_patience));

        // 写回进行到一半时取代：取代要等写回结束才返回，这样它之后的写入或删除一定排在这次写回后面
        var write = Task.Run(() => handle.TryWrite("first", _ =>
        {
            writing.Set();
            Assert.True(finish.Wait(_patience));
            _cache["k"] = "first";
        }));
        Assert.True(writing.Wait(_patience));
        var supersede = Task.Run(handle.Supersede);
        Assert.NotSame(supersede, await Task.WhenAny(supersede, Task.Delay(TimeSpan.FromMilliseconds(200))));
        Assert.False(handle.IsSuperseded);

        finish.Set();
        Assert.True(await write.WaitAsync(_patience));
        await supersede.WaitAsync(_patience);
        Assert.True(handle.IsSuperseded);

        // 取代之后不再写
        Assert.False(handle.TryWrite("second", written => _cache["k"] = written));
        Assert.Equal("first", _cache["k"]);
    }

    [Fact]
    public async Task DifferentResultTypes_ForTheSameKey_AreNotMerged()
    {
        var key = new CacheKey("k");
        var text = new TaskCompletionSource<string>();
        var asText = _table.RunAsync(key, (_, _) => new ValueTask<string>(text.Task), CancellationToken.None).AsTask();

        // 同一个键被以别的类型读取：拿不到在途那次的值，不等它，照没有合并时自己回源
        Assert.Equal(42, await _table.RunAsync(key, static (_, _) => ValueTask.FromResult(42), CancellationToken.None).AsTask().WaitAsync(_patience));
        Assert.False(asText.IsCompleted);
        // 没有登记的那次结束时不会把别人的条目移走
        Assert.Equal(1, _table.Count);

        text.SetResult("text");
        Assert.Equal("text", await asText.WaitAsync(_patience));
        Assert.Equal(0, _table.Count);
    }

    [Fact]
    public async Task ReadingAKeyFromInsideItsOwnLoad_DoesNotWaitForItself()
    {
        var outer = new CacheKey("outer");
        var inner = new CacheKey("inner");
        var order = new ConcurrentQueue<string>();

        // 回源函数里再读自己正在回源的键，或经另一个键绕回来：等下去是等自己，改为直接回源
        var value = await _table.RunAsync(outer, async (_, token) =>
        {
            order.Enqueue("outer");
            var nested = await _table.RunAsync(inner, async (_, innerToken) =>
            {
                order.Enqueue("inner");
                await Task.Yield();
                return await _table.RunAsync(outer, (_, _) =>
                {
                    order.Enqueue("outer-again");
                    return ValueTask.FromResult("leaf");
                }, innerToken);
            }, token);
            var self = await _table.RunAsync(outer, static (_, _) => ValueTask.FromResult("self"), token);
            return $"{nested}+{self}";
        }, CancellationToken.None).AsTask().WaitAsync(_patience);

        Assert.Equal("leaf+self", value);
        Assert.Equal(["outer", "inner", "outer-again"], order);
        Assert.Equal(0, _table.Count);

        // 调用链之外的调用照常合并：回源函数结束之后，同一条执行流上的下一次调用不受上一次的影响
        var gate = new TaskCompletionSource<string>();
        var leader = _table.RunAsync(outer, (_, _) => new ValueTask<string>(gate.Task), CancellationToken.None).AsTask();
        var waiter = _table.RunAsync<string>(outer, static (_, _) => throw new InvalidOperationException("A waiter should not load."), CancellationToken.None).AsTask();
        Assert.False(waiter.IsCompleted);
        gate.SetResult("merged");
        Assert.Equal(["merged", "merged"], await Task.WhenAll(leader, waiter).WaitAsync(_patience));
    }

    [Fact]
    public async Task ManyThreads_ManyKeys_EachKeyLoadsOnce_AndTheTableEndsEmpty()
    {
        const int Rounds = 20;
        const int Keys = 8;
        const int CallersPerKey = 32;
        for (var round = 0; round < Rounds; round++)
        {
            // 异步的起跑信号：不让几百个任务各占一个线程池线程干等
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = new List<Task<string>>(Keys * CallersPerKey);
            for (var caller = 0; caller < CallersPerKey; caller++)
            {
                for (var index = 0; index < Keys; index++)
                {
                    var key = new CacheKey($"round-{round}:key-{index}");
                    calls.Add(Task.Run(async () =>
                    {
                        await start.Task;
                        return await GetOrLoadAsync(key, static async token =>
                        {
                            // 让出线程再回来：领头的回源还在途中时别的调用有机会撞上它
                            await Task.Yield();
                            await Task.Delay(1, token);
                            return Guid.NewGuid().ToString("N");
                        }, CancellationToken.None);
                    }));
                }
            }

            start.SetResult();
            var values = await Task.WhenAll(calls).WaitAsync(_patience);

            // 每个键只查了一次源，所有调用拿到的是同一个值
            for (var index = 0; index < Keys; index++)
            {
                var name = $"round-{round}:key-{index}";
                Assert.Equal(1, _sourceCalls[name]);
                Assert.Single(values.Where((_, position) => position % Keys == index).Distinct());
            }

            Assert.Equal(0, _table.Count);
        }
    }

    /// <summary>
    /// 照缓存实现的写法取或建
    /// </summary>
    /// <remarks>
    /// 命中不进在途表；未命中时领头的调用再读一次，仍未命中才查源并写回
    /// </remarks>
    private ValueTask<string> GetOrLoadAsync(CacheKey key, Func<CancellationToken, ValueTask<string>> source, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key.Value, out var hit))
        {
            return ValueTask.FromResult(hit);
        }

        return _table.RunAsync(key, async (load, token) =>
        {
            if (_cache.TryGetValue(key.Value, out var written))
            {
                return written;
            }

            _sourceCalls.AddOrUpdate(key.Value, 1, static (_, count) => count + 1);
            var value = await source(token);
            load.TryWrite((Cache: _cache, Key: key.Value, Value: value), static entry => entry.Cache[entry.Key] = entry.Value);
            return value;
        }, cancellationToken);
    }

    /// <summary>
    /// 轮询到条件成立
    /// </summary>
    /// <remarks>
    /// 交接发生在线程池上，没有可以等的信号；超时即失败
    /// </remarks>
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
