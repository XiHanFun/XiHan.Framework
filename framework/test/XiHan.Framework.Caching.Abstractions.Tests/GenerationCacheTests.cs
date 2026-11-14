// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Serialization;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 代数缓存区（ADR-0229）
/// </summary>
/// <remarks>
/// 批量读穿与去重、按范围与整区失效、失效之前开始的读取不算命中（同一个实例与共用缓存的另一个实例）、缺代数先换新、查源失败不缓存、缓存后端故障时直接查源、键的编码。底层是架在字节存储上的序列化缓存，条目经源生成上下文序列化
/// </remarks>
public sealed class GenerationCacheTests
{
    /// <summary>
    /// 字节存储
    /// </summary>
    private readonly FakeCacheStore _store = new();

    /// <summary>
    /// 被测缓存区底下的类型化缓存
    /// </summary>
    private readonly ICache _cache;

    /// <summary>
    /// 被测缓存区
    /// </summary>
    private readonly GenerationCache<string[]> _region;

    /// <summary>
    /// 每次查源收到的位置
    /// </summary>
    private readonly List<string> _loads = [];

    /// <summary>
    /// 数据源：位置 → 值
    /// </summary>
    private readonly Dictionary<GenerationCacheKey, string[]> _source = [];

    /// <summary>
    /// 创建
    /// </summary>
    public GenerationCacheTests()
    {
        _cache = new SerializingCache(_store, new JsonCacheSerializer(new JsonSerializerOptions { TypeInfoResolver = GenerationCacheJsonContext.Default }));
        _region = new GenerationCache<string[]>(_cache, "test", TimeSpan.FromSeconds(30), NullLogger.Instance);
    }

    [Fact]
    public async Task GetOrLoadMany_LoadsDistinctMissesOnce_ThenServesHitsInInputOrder()
    {
        _source[Key("a", "x")] = ["1"];
        _source[Key("b", "y")] = ["2"];

        Assert.Empty((await _region.GetOrLoadManyAsync([], LoadAsync, CancellationToken.None)).ValueOrThrow());
        Assert.Empty(_store.Operations);

        var first = (await _region.GetOrLoadManyAsync([Key("a", "x"), Key("b", "y"), Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["1", "2", "1"], first.Select(static v => v[0]));
        Assert.Equal(["a/x,b/y"], _loads);

        // 源变了但没有失效：命中缓存
        _source[Key("a", "x")] = ["changed"];
        var second = (await _region.GetOrLoadManyAsync([Key("b", "y"), Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["2", "1"], second.Select(static v => v[0]));
        Assert.Single(_loads);
    }

    [Fact]
    public async Task Invalidate_DropsEveryEntryOfTheScope_AndInvalidateAllDropsEverything()
    {
        _source[Key("a", "x")] = ["ax"];
        _source[Key("a", "y")] = ["ay"];
        _source[Key("b", "x")] = ["bx"];
        await _region.GetOrLoadManyAsync([Key("a", "x"), Key("a", "y"), Key("b", "x")], LoadAsync, CancellationToken.None);
        _loads.Clear();

        // 空集合什么也不做；按范围失效让该范围下全部条目重查，别的范围仍命中
        await _region.InvalidateAsync([], CancellationToken.None);
        await _region.InvalidateAsync(["a", "a"], CancellationToken.None);
        await _region.GetOrLoadManyAsync([Key("a", "x"), Key("a", "y"), Key("b", "x")], LoadAsync, CancellationToken.None);
        Assert.Equal(["a/x,a/y"], _loads);

        _loads.Clear();
        await _region.InvalidateAllAsync(CancellationToken.None);
        await _region.GetOrLoadManyAsync([Key("a", "x"), Key("b", "x")], LoadAsync, CancellationToken.None);
        Assert.Equal(["a/x,b/x"], _loads);
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _region.InvalidateAsync([null!], CancellationToken.None));
    }

    [Fact]
    public async Task ReadStartedBeforeAnInvalidation_IsNotServedAsAHit_EvenFromAnotherInstance()
    {
        // 另一个实例：同一份底层缓存上的另一个缓存区对象
        var other = new GenerationCache<string[]>(_cache, "test", TimeSpan.FromSeconds(30), NullLogger.Instance);
        _source[Key("a", "x")] = ["old"];

        // 查源的途中有写入并失效（失效之前开始的读取读到提交之前的旧值）：结果照常返回，但写回的条目带着旧代数
        var read = await _region.GetOrLoadManyAsync([Key("a", "x")], async (keys, ct) =>
        {
            var stale = await LoadAsync(keys, ct);
            _source[Key("a", "x")] = ["new"];
            await other.InvalidateAsync(["a"], ct);
            return stale;
        }, CancellationToken.None);
        Assert.Equal("old", read.ValueOrThrow()[0][0]);

        // 两个实例的下一次读取都不把它当作命中
        Assert.Equal("new", (await other.GetOrLoadManyAsync([Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow()[0][0]);
        _source[Key("a", "x")] = ["newer"];
        await _region.InvalidateAsync(["a"], CancellationToken.None);
        Assert.Equal("newer", (await _region.GetOrLoadManyAsync([Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow()[0][0]);
    }

    [Fact]
    public async Task LostGenerations_AreMintedAnew_SoOldEntriesStopCounting()
    {
        _source[Key("a", "x")] = ["old"];
        await _region.GetOrLoadManyAsync([Key("a", "x")], LoadAsync, CancellationToken.None);
        _source[Key("a", "x")] = ["new"];

        // 代数被淘汰或过期（条目还在）：下一次读取换新代数，旧条目作废，不会被当作命中
        await _store.RemoveAsync(new CacheKey("test:gen:1.a"), CancellationToken.None);
        Assert.Equal("new", (await _region.GetOrLoadManyAsync([Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow()[0][0]);
        _source[Key("a", "x")] = ["newer"];
        await _store.RemoveAsync(new CacheKey("test:gen"), CancellationToken.None);
        Assert.Equal("newer", (await _region.GetOrLoadManyAsync([Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow()[0][0]);
    }

    [Fact]
    public async Task LoadFailure_IsReturned_AndNothingIsCached()
    {
        var unavailable = Error.Unavailable("TEST_UNAVAILABLE_001", "test.source.unavailable");
        var failed = await _region.GetOrLoadManyAsync([Key("a", "x")], (_, _) => ValueTask.FromResult<Result<IReadOnlyList<string[]>>>(unavailable), CancellationToken.None);
        Assert.Equal("TEST_UNAVAILABLE_001", failed.Error?.Code);

        // 源恢复后立即重查，而不是在存活时间内一直报失败
        _source[Key("a", "x")] = ["ok"];
        Assert.Equal("ok", (await _region.GetOrLoadManyAsync([Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow()[0][0]);
        Assert.Single(_loads);

        // 查源返回的条数不对是编程错误
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _region.GetOrLoadManyAsync([Key("z", "z")], (_, _) => ValueTask.FromResult(Result.Success<IReadOnlyList<string[]>>(Array.Empty<string[]>())), CancellationToken.None));
    }

    [Fact]
    public async Task CacheOutage_ReadsGoStraightToTheSource_AndInvalidationThrows()
    {
        var faulty = new FaultyCache(_cache);
        var region = new GenerationCache<string[]>(faulty, "test", TimeSpan.FromSeconds(30), NullLogger.Instance);
        _source[Key("a", "x")] = ["1"];
        await region.GetOrLoadManyAsync([Key("a", "x")], LoadAsync, CancellationToken.None);

        // 后端不可达：每次读取都直接查源，结果仍然正确、不写回
        faulty.Failing = true;
        _source[Key("a", "x")] = ["2"];
        Assert.Equal("2", (await region.GetOrLoadManyAsync([Key("a", "x"), Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow()[1][0]);
        Assert.Equal("2", (await region.GetOrLoadManyAsync([Key("a", "x")], LoadAsync, CancellationToken.None)).ValueOrThrow()[0][0]);
        Assert.Equal(3, _loads.Count);

        // 失效传不出去：抛给调用方，由它决定拒绝写入
        var thrown = await Assert.ThrowsAsync<FrameworkException>(async () => await region.InvalidateAsync(["a"], CancellationToken.None));
        Assert.Equal(CachingErrors.StoreUnavailable, thrown.Code);
        await Assert.ThrowsAsync<FrameworkException>(async () => await region.InvalidateAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ScopesAndEntries_AreEncodedIntoDistinctValidKeys()
    {
        var long1 = new string('x', 600);
        var spaced = "role with spaces";
        _source[Key("a:b", "c")] = ["1"];
        _source[Key("a", "b:c")] = ["2"];
        _source[Key(long1, "")] = ["3"];
        _source[Key(spaced, "t")] = ["4"];

        // 冒号拼接会让 a:b + c 与 a + b:c 撞键；按长度前缀不会。含空白或过长的部分换成摘要
        var values = (await _region.GetOrLoadManyAsync([Key("a:b", "c"), Key("a", "b:c"), Key(long1, ""), Key(spaced, "t")], LoadAsync, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["1", "2", "3", "4"], values.Select(static v => v[0]));
        Assert.Equal(["1", "2", "3", "4"], (await _region.GetOrLoadManyAsync([Key("a:b", "c"), Key("a", "b:c"), Key(long1, ""), Key(spaced, "t")], LoadAsync, CancellationToken.None)).ValueOrThrow().Select(static v => v[0]));
        Assert.Single(_loads);
        Assert.Contains(_store.Operations, static op => op.Contains("test:entry:3.a:b:1.c", StringComparison.Ordinal));
        Assert.Contains(_store.Operations, static op => op.Contains("test:entry:1.a:3.b:c", StringComparison.Ordinal));
        Assert.DoesNotContain(_store.Operations, op => op.Contains(long1, StringComparison.Ordinal) || op.Contains(spaced, StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_RejectsInvalidRegionAndTimeToLive()
    {
        Assert.Throws<ArgumentException>(() => new GenerationCache<string[]>(_cache, "has space", TimeSpan.FromSeconds(1), NullLogger.Instance));
        Assert.Throws<ArgumentException>(() => new GenerationCache<string[]>(_cache, new string('r', GenerationCache<string[]>.MaxRegionLength + 1), TimeSpan.FromSeconds(1), NullLogger.Instance));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GenerationCache<string[]>(_cache, "ok", TimeSpan.Zero, NullLogger.Instance));
        Assert.Equal(TimeSpan.MaxValue / 2, new GenerationCache<string[]>(_cache, "ok", TimeSpan.MaxValue / 2, NullLogger.Instance).TimeToLive);
        Assert.Throws<InvalidOperationException>(static () => default(GenerationCacheKey).Scope);
        Assert.Equal(string.Empty, GenerationCacheKey.ForScope("s").Entry);
    }

    /// <summary>
    /// 范围与条目组成的位置
    /// </summary>
    private static GenerationCacheKey Key(string scope, string entry) => new(scope, entry);

    /// <summary>
    /// 从 <see cref="_source"/> 查源，并记下收到的位置
    /// </summary>
    private ValueTask<Result<IReadOnlyList<string[]>>> LoadAsync(IReadOnlyList<GenerationCacheKey> keys, CancellationToken cancellationToken)
    {
        _loads.Add(string.Join(",", keys.Select(static k => k.Scope.Length > 20 ? "long/" + k.Entry : k.Scope + "/" + k.Entry)));
        return ValueTask.FromResult(Result.Success<IReadOnlyList<string[]>>(keys.Select(k => _source[k]).ToList()));
    }
}
