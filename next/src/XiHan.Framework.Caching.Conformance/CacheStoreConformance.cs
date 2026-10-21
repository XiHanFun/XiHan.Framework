// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.Caching.Conformance;

/// <summary>
/// <see cref="ICacheStore"/> 的一致性套件。继承并实现 <see cref="CreateStore"/>，全部用例随之执行。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class CacheStoreConformance
{
    /// <summary>
    /// 过期用例在存活时间之外额外等待的时间，给 Provider 的过期粒度留余量。
    /// </summary>
    protected virtual TimeSpan ExpirationGrace => TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// 创建被测存储。每个用例调用一次；返回的存储不应含有本套件使用的键。
    /// </summary>
    protected abstract ICacheStore CreateStore();

    /// <summary>
    /// 用例 1：写入后能读到同样的字节；没写过的键为无值。
    /// </summary>
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
    /// 用例 2：再次写入覆盖旧值；键区分大小写。
    /// </summary>
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
    /// 用例 8：滑动过期在读取时延长；不再读取后到期。
    /// </summary>
    [Fact]
    public async Task SlidingExpiration_ExtendsOnRead_ThenExpires()
    {
        var store = CreateStore();
        var key = Key();
        var window = TimeSpan.FromMilliseconds(800);
        await store.SetAsync(key, Bytes("sliding"), CacheEntryOptions.Sliding(window), CancellationToken.None).ConfigureAwait(false);

        await Task.Delay(window / 2).ConfigureAwait(false);
        Assert.True((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue);
        await Task.Delay(window / 2 + ExpirationGrace / 2).ConfigureAwait(false);
        Assert.True((await store.GetAsync(key, CancellationToken.None).ConfigureAwait(false)).HasValue, "读取应延长滑动过期");

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
            Assert.True((await locks.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).HasValue, "到期后应能被他人获取");
            Assert.False(await second.ReleaseAsync(CancellationToken.None).ConfigureAwait(false), "到期后令牌不再匹配，释放失败");
        }
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
    /// 读取结果转文本；无值抛出。
    /// </summary>
    protected static string Text(Maybe<ReadOnlyMemory<byte>> value) => Encoding.UTF8.GetString(value.ValueOrThrow().Span);
}
