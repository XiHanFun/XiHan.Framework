// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.Caching.Conformance;

/// <summary>
/// <see cref="ICacheStore"/> 的一致性套件（含批量用例）。
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateStore"/>，全部用例随之执行。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class CacheStoreConformance
{
    /// <summary>
    /// 过期用例在存活时间之外额外等待的时间，给 Provider 的过期粒度留余量。
    /// </summary>
    protected virtual TimeSpan ExpirationGrace => TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// 创建被测存储。
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次；返回的存储不应含有本套件使用的键。
    /// </remarks>
    protected abstract ICacheStore CreateStore();

    /// <summary>
    /// 用例 1：写入后能读到同样的字节。
    /// </summary>
    /// <remarks>
    /// 没写过的键为无值。
    /// </remarks>
    [Fact]
    public async Task Set_ThenGet_RoundTrips_AndMissingIsNone()
    {
        var store = CreateStore();
        var key = Key();

        await store.SetAsync(key, Bytes("hello"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false);

        var hit = await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false);
        Assert.Equal("hello", Text(hit));
        Assert.False((await store.GetAsync(Key(), CancellationToken.None).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 2：再次写入覆盖旧值。
    /// </summary>
    /// <remarks>
    /// 键区分大小写。
    /// </remarks>
    [Fact]
    public async Task Set_Overwrites_AndKeysAreCaseSensitive()
    {
        var store = CreateStore();
        var key = Key();
        var upper = new CacheKey(key.Value.ToUpperInvariant());

        await store.SetAsync(key, Bytes("v1"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        await store.SetAsync(key, Bytes("v2"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false);

        Assert.Equal("v2", Text(await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)));
        Assert.False((await store.GetAsync(upper, CancellationToken.None).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 3：删除报告是否存在，删除后读不到。
    /// </summary>
    [Fact]
    public async Task Remove_ReportsExistence()
    {
        var store = CreateStore();
        var key = Key();
        await store.SetAsync(key, Bytes("x"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false);

        Assert.True(await store.RemoveAsync(key, CancellationToken.None).ConfigureAwait(false));
        Assert.False(await store.RemoveAsync(key, CancellationToken.None).ConfigureAwait(false));
        Assert.False((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 4：不存在才写——首次成功，之后失败，删除后又能写。
    /// </summary>
    [Fact]
    public async Task TryAdd_OnlyFirstWins_UntilRemoved()
    {
        var store = CreateStore();
        var key = Key();

        Assert.True(await store.TryAddAsync(key, Bytes("first"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false));
        Assert.False(await store.TryAddAsync(key, Bytes("second"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false));
        Assert.Equal("first", Text(await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)));

        await store.RemoveAsync(key, CancellationToken.None).ConfigureAwait(false);
        Assert.True(await store.TryAddAsync(key, Bytes("third"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 5：并发的不存在才写恰好一个成功。
    /// </summary>
    [Fact]
    public async Task TryAdd_Concurrent_ExactlyOneWins()
    {
        var store = CreateStore();
        var key = Key();

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(async i =>
        {
            await Task.Yield();
            return await store.TryAddAsync(key, Bytes($"w{i}"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        })).ConfigureAwait(false);

        Assert.Equal(1, results.Count(static r => r));
    }

    /// <summary>
    /// 用例 6：比较删只在值相同时删除。
    /// </summary>
    [Fact]
    public async Task TryRemove_RequiresMatchingValue()
    {
        var store = CreateStore();
        var key = Key();
        await store.SetAsync(key, Bytes("token-a"), CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false);

        Assert.False(await store.TryRemoveAsync(key, Bytes("token-b"), CancellationToken.None).ConfigureAwait(false));
        Assert.True((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue);
        Assert.True(await store.TryRemoveAsync(key, Bytes("token-a"), CancellationToken.None).ConfigureAwait(false));
        Assert.False((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue);
        Assert.False(await store.TryRemoveAsync(key, Bytes("token-a"), CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 7：绝对存活时间到期后读不到，也能重新"不存在才写"。
    /// </summary>
    [Fact]
    public async Task TimeToLive_ExpiresEntry()
    {
        var store = CreateStore();
        var key = Key();
        var timeToLive = TimeSpan.FromMilliseconds(400);

        Assert.True(await store.TryAddAsync(key, Bytes("short"), CacheEntryOptions.Expires(timeToLive), CancellationToken.None).ConfigureAwait(false));
        Assert.True((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue);

        await Task.Delay(timeToLive + ExpirationGrace).ConfigureAwait(false);

        Assert.False((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue);
        Assert.True(await store.TryAddAsync(key, Bytes("again"), CacheEntryOptions.Expires(timeToLive), CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 8：滑动过期在读取时延长。
    /// </summary>
    /// <remarks>
    /// 不再读取后到期。
    /// </remarks>
    [Fact]
    public async Task SlidingExpiration_ExtendsOnRead_ThenExpires()
    {
        var store = CreateStore();
        var key = Key();
        var window = TimeSpan.FromMilliseconds(800);
        await store.SetAsync(key, Bytes("sliding"), CacheEntryOptions.Sliding(window), CancellationToken.None).ConfigureAwait(false);

        await Task.Delay(window / 2).ConfigureAwait(false);
        Assert.True((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue);
        // 第二次读取发生在原始窗口之外：只有第一次读取延长过到期时刻才仍然命中
        await Task.Delay(window / 2 + ExpirationGrace / 2).ConfigureAwait(false);
        Assert.True((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue, "Reading should extend the sliding expiration.");

        await Task.Delay(window + ExpirationGrace).ConfigureAwait(false);
        Assert.False((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 9：大值完整往返。
    /// </summary>
    [Fact]
    public async Task LargeValue_RoundTrips()
    {
        var store = CreateStore();
        var key = Key();
        var payload = new byte[256 * 1024];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 251);
        }

        await store.SetAsync(key, payload, CacheEntryOptions.Default, CancellationToken.None).ConfigureAwait(false);

        var hit = await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false);
        Assert.True(hit.HasValue);
        Assert.True(payload.AsSpan().SequenceEqual(hit.ValueOrThrow().Span));
    }

    /// <summary>
    /// 用例 10：基于存储的分布式锁互斥、可释放、释放后可再获取、到期自动释放。
    /// </summary>
    [Fact]
    public async Task DistributedLock_IsExclusive_ReleasableAndExpiring()
    {
        var store = CreateStore();
        var locks = new StoreDistributedLock(store);
        var resource = Key();

        var first = await locks.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
        Assert.True(first.HasValue);
        Assert.False((await locks.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).HasValue);

        Assert.True(await first.ValueOrThrow().ReleaseAsync(CancellationToken.None).ConfigureAwait(false));
        Assert.False(await first.ValueOrThrow().ReleaseAsync(CancellationToken.None).ConfigureAwait(false));

        var timeToLive = TimeSpan.FromMilliseconds(400);
        await using (var second = (await locks.TryAcquireAsync(resource, timeToLive, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow())
        {
            Assert.Equal(resource, second.Resource);
            await Task.Delay(timeToLive + ExpirationGrace).ConfigureAwait(false);
            Assert.True((await locks.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).HasValue, "After expiry, the lock should be acquirable by others.");
            Assert.False(await second.ReleaseAsync(CancellationToken.None).ConfigureAwait(false), "After expiry, the token no longer matches and release should fail.");
        }
    }

    /// <summary>
    /// 用例 11：批量写入后批量读取。
    /// </summary>
    /// <remarks>
    /// 结果与键顺序一一对应、未命中为无值；同一键在一批里出现多次后者覆盖。
    /// </remarks>
    [Fact]
    public async Task SetMany_ThenGetMany_KeepsOrder_AndLastWriteWins()
    {
        var store = CreateStore();
        var first = Key();
        var second = Key();

        await store.SetManyAsync(
        [
            new CacheEntry(first, Bytes("1")),
            new CacheEntry(second, Bytes("2"), CacheEntryOptions.Expires(TimeSpan.FromMinutes(1))),
            new CacheEntry(first, Bytes("1b")),
        ], CancellationToken.None).ConfigureAwait(false);

        var hits = await store.GetManyAsync([second, Key(), first, second], CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(4, hits.Count);
        Assert.Equal("2", Text(hits[0]));
        Assert.False(hits[1].HasValue);
        Assert.Equal("1b", Text(hits[2]));
        Assert.Equal("2", Text(hits[3]));
        Assert.Equal("1b", Text(await store.GetAsync(first, CancellationToken.None).ConfigureAwait(false)));
    }

    /// <summary>
    /// 用例 12：批量删除逐键报告是否存在，同一键出现多次第二次为假。
    /// </summary>
    /// <remarks>
    /// 之后批量读取都未命中。
    /// </remarks>
    [Fact]
    public async Task RemoveMany_ReportsPerKey()
    {
        var store = CreateStore();
        var first = Key();
        var second = Key();
        await store.SetManyAsync([new CacheEntry(first, Bytes("1")), new CacheEntry(second, Bytes("2"))], CancellationToken.None).ConfigureAwait(false);

        var removed = await store.RemoveManyAsync([first, first, second, Key()], CancellationToken.None).ConfigureAwait(false);

        Assert.Equal([true, false, true, false], removed);
        Assert.All(await store.GetManyAsync([first, second], CancellationToken.None).ConfigureAwait(false), static hit => Assert.False(hit.HasValue));
    }

    /// <summary>
    /// 用例 13：空集合返回空结果，不触后端也不抛出。
    /// </summary>
    [Fact]
    public async Task BatchMethods_EmptyInput_ReturnEmpty()
    {
        var store = CreateStore();

        await store.SetManyAsync([], CancellationToken.None).ConfigureAwait(false);

        Assert.Empty(await store.GetManyAsync([], CancellationToken.None).ConfigureAwait(false));
        Assert.Empty(await store.RemoveManyAsync([], CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 14：批量写入的每项各自过期。
    /// </summary>
    /// <remarks>
    /// 短存活的到期后读不到、长存活的仍在；批量读取同样延长滑动过期。
    /// </remarks>
    [Fact]
    public async Task SetMany_HonoursPerEntryExpiration_AndGetManyExtendsSliding()
    {
        var store = CreateStore();
        var shortLived = Key();
        var longLived = Key();
        var sliding = Key();
        var timeToLive = TimeSpan.FromMilliseconds(400);
        // 滑动窗口取首次读取时刻的两倍：首次批量读取落在窗口中点，第二次读取落在原始窗口之外、延长后的窗口之内
        var window = 2 * (timeToLive + ExpirationGrace);
        await store.SetManyAsync(
        [
            new CacheEntry(shortLived, Bytes("s"), CacheEntryOptions.Expires(timeToLive)),
            new CacheEntry(longLived, Bytes("l"), CacheEntryOptions.Expires(TimeSpan.FromSeconds(30))),
            new CacheEntry(sliding, Bytes("w"), CacheEntryOptions.Sliding(window)),
        ], CancellationToken.None).ConfigureAwait(false);

        await Task.Delay(timeToLive + ExpirationGrace).ConfigureAwait(false);
        var hits = await store.GetManyAsync([shortLived, longLived, sliding], CancellationToken.None).ConfigureAwait(false);
        Assert.False(hits[0].HasValue);
        Assert.Equal("l", Text(hits[1]));
        Assert.Equal("w", Text(hits[2]));

        // 第二次读取发生在原始窗口之外：只有上面的批量读取延长过到期时刻才仍然命中
        await Task.Delay(window / 2 + ExpirationGrace / 2).ConfigureAwait(false);
        Assert.True((await store.GetManyAsync([sliding], CancellationToken.None).ConfigureAwait(false))[0].HasValue, "Batch reads should extend the sliding expiration.");
    }

    /// <summary>
    /// 新的随机键。
    /// </summary>
    protected static CacheKey Key() => new($"conformance:{Guid.NewGuid():N}");

    /// <summary>
    /// UTF-8 字节。
    /// </summary>
    protected static ReadOnlyMemory<byte> Bytes(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>
    /// 读取结果转文本。
    /// </summary>
    /// <remarks>
    /// 无值抛出。
    /// </remarks>
    protected static string Text(Maybe<ReadOnlyMemory<byte>> value) => Encoding.UTF8.GetString(value.ValueOrThrow().Span);
}
