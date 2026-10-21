// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Caching.Abstractions;

namespace XiHan.Framework.Caching.InMemory.Tests;

/// <summary>
/// <see cref="InMemoryCacheStore"/> 的单元测试：用假时钟验证 LRU 淘汰、默认与滑动过期、不存在才写、字节隔离与选项解析。
/// </summary>
public sealed class InMemoryCacheStoreTests
{
    /// <summary>
    /// 假时钟的起始时刻。
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 文本转 UTF-8 字节。
    /// </summary>
    private static ReadOnlyMemory<byte> Bytes(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>
    /// 创建带假时钟的存储；默认容量 100、默认存活 5 分钟。
    /// </summary>
    private static (InMemoryCacheStore Store, FakeTimeProvider Clock) Arrange(int maxEntries = 100, TimeSpan? defaultTtl = null)
    {
        var clock = new FakeTimeProvider(_now);
        return (new InMemoryCacheStore(new InMemoryCachingOptions { MaxEntries = maxEntries, DefaultTimeToLive = defaultTtl ?? TimeSpan.FromMinutes(5) }, clock), clock);
    }

    [Fact]
    public async Task Capacity_EvictsLeastRecentlyUsed()
    {
        var (store, _) = Arrange(maxEntries: 2);
        await store.SetAsync("a", Bytes("1"), CacheEntryOptions.Default, CancellationToken.None);
        await store.SetAsync("b", Bytes("2"), CacheEntryOptions.Default, CancellationToken.None);
        _ = await store.GetAsync("a", CancellationToken.None);

        await store.SetAsync("c", Bytes("3"), CacheEntryOptions.Default, CancellationToken.None);

        Assert.Equal(2, store.Count);
        Assert.True((await store.GetAsync("a", CancellationToken.None)).HasValue);
        Assert.False((await store.GetAsync("b", CancellationToken.None)).HasValue);
        Assert.True((await store.GetAsync("c", CancellationToken.None)).HasValue);
    }

    [Fact]
    public async Task DefaultTimeToLive_AppliesWhenOptionsSilent()
    {
        var (store, clock) = Arrange(defaultTtl: TimeSpan.FromSeconds(30));
        await store.SetAsync("k", Bytes("v"), CacheEntryOptions.Default, CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.True((await store.GetAsync("k", CancellationToken.None)).HasValue);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False((await store.GetAsync("k", CancellationToken.None)).HasValue);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task SlidingExpiration_RefreshesOnRead_WithFakeClock()
    {
        var (store, clock) = Arrange();
        await store.SetAsync("k", Bytes("v"), CacheEntryOptions.Sliding(TimeSpan.FromMinutes(1)), CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(50));
        Assert.True((await store.GetAsync("k", CancellationToken.None)).HasValue);
        clock.Advance(TimeSpan.FromSeconds(50));
        Assert.True((await store.GetAsync("k", CancellationToken.None)).HasValue);
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.False((await store.GetAsync("k", CancellationToken.None)).HasValue);
    }

    [Fact]
    public async Task TryAdd_SucceedsOverExpiredEntry_AndSetRejectsConflictingOptions()
    {
        var (store, clock) = Arrange();
        Assert.True(await store.TryAddAsync("k", Bytes("old"), CacheEntryOptions.Expires(TimeSpan.FromSeconds(1)), CancellationToken.None));
        Assert.False(await store.TryAddAsync("k", Bytes("new"), CacheEntryOptions.Expires(TimeSpan.FromSeconds(1)), CancellationToken.None));

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.True(await store.TryAddAsync("k", Bytes("new"), CacheEntryOptions.Expires(TimeSpan.FromSeconds(1)), CancellationToken.None));
        Assert.Equal("new", Encoding.UTF8.GetString((await store.GetAsync("k", CancellationToken.None)).ValueOrThrow().Span));
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.SetAsync("k", Bytes("x"), new CacheEntryOptions { TimeToLive = TimeSpan.FromSeconds(1), SlidingExpiration = TimeSpan.FromSeconds(1) }, CancellationToken.None));
    }

    [Fact]
    public async Task Get_ReturnsCopyIsolatedFromCallerBuffer()
    {
        var (store, _) = Arrange();
        var buffer = Encoding.UTF8.GetBytes("original");
        await store.SetAsync("k", buffer, CacheEntryOptions.Default, CancellationToken.None);

        buffer[0] = (byte)'X';

        Assert.Equal("original", Encoding.UTF8.GetString((await store.GetAsync("k", CancellationToken.None)).ValueOrThrow().Span));
        store.Clear();
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        // 用内存键值对构建配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        var options = InMemoryCachingOptions.FromConfiguration(Build(("XiHan:Caching:InMemory:MaxEntries", "5"), ("XiHan:Caching:InMemory:DefaultTimeToLiveSeconds", "2.5")));

        Assert.Equal(5, options.MaxEntries);
        Assert.Equal(TimeSpan.FromSeconds(2.5), options.DefaultTimeToLive);
        Assert.Equal(10_000, InMemoryCachingOptions.FromConfiguration(Build()).MaxEntries);
        Assert.Throws<ArgumentException>(() => InMemoryCachingOptions.FromConfiguration(Build(("XiHan:Caching:InMemory:MaxEntries", "0"))));
        Assert.Throws<ArgumentException>(() => InMemoryCachingOptions.FromConfiguration(Build(("XiHan:Caching:InMemory:DefaultTimeToLiveSeconds", "soon"))));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new InMemoryCachingOptions { MaxEntries = 0 });
    }
}
