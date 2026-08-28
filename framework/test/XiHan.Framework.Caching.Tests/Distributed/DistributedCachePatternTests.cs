// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 分布式缓存的键模式与脚本能力测试
/// </summary>
/// <remarks>
/// 这几个方法是「能力接口 + 规范化键」的组合点：对外收发的是业务键，对内收发的是带租户与缓存名前缀的规范化键。
/// 前缀剥离一旦出错，按模式清理会误删别的缓存名甚至别的租户的数据，所以这里逐项锁死。
/// </remarks>
public class DistributedCachePatternTests
{
    private const string SamplePrefix = "0:sample:";

    /// <summary>
    /// 按模式取键时把规范化前缀剥掉，只返回业务键
    /// </summary>
    [Fact]
    public void GetKeys_WithPatternCapableBackend_StripsNormalizedPrefix()
    {
        var store = new FakeCapableDistributedCacheStore
        {
            PatternKeys = [SamplePrefix + "a", SamplePrefix + "b", "external", SamplePrefix, "   ", SamplePrefix + "a"]
        };
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var keys = cache.GetKeys();

        // 只剩下前缀的键剥离后为空、纯空白键无意义，都应被丢弃；重复键去重后保持首次出现顺序
        Assert.Equal(new[] { "a", "b", "external" }, keys);
    }

    /// <summary>
    /// 业务模式在下发给后端前被规范化为带前缀的模式
    /// </summary>
    [Fact]
    public void GetKeys_PassesNormalizedPatternToBackend()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.GetKeys("user:*");

        Assert.Equal(SamplePrefix + "user:*", store.LastPattern);
    }

    /// <summary>
    /// 空白模式回落为通配全部
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetKeys_WithBlankPattern_FallsBackToMatchAll(string pattern)
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.GetKeys(pattern);

        Assert.Equal(SamplePrefix + "*", store.LastPattern);
    }

    /// <summary>
    /// 模式两端的空白在规范化前被裁剪
    /// </summary>
    [Fact]
    public void GetKeys_TrimsPatternBeforeNormalizing()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.GetKeys("  user:*  ");

        Assert.Equal(SamplePrefix + "user:*", store.LastPattern);
    }

    /// <summary>
    /// 后端不支持键模式时按模式取键返回空集合
    /// </summary>
    [Fact]
    public void GetKeys_WithBackendLackingPatternSupport_ReturnsEmpty()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Empty(cache.GetKeys());
    }

    /// <summary>
    /// 异步按模式取键与同步保持一致的剥离语义
    /// </summary>
    [Fact]
    public async Task GetKeysAsync_WithPatternCapableBackend_StripsNormalizedPrefix()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeCapableDistributedCacheStore
        {
            PatternKeys = [SamplePrefix + "a"]
        };
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var keys = await cache.GetKeysAsync(token: token);

        Assert.Equal(new[] { "a" }, keys);
    }

    /// <summary>
    /// 非字符串键类型不支持按模式操作，未隐藏异常时直接抛出
    /// </summary>
    [Fact]
    public void GetKeys_ForNonStringKeyType_ThrowsWhenErrorsNotHidden()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.Create<SampleCacheItem, int>();

        Assert.Throws<NotSupportedException>(() => cache.GetKeys(hideErrors: false));
    }

    /// <summary>
    /// 非字符串键类型在隐藏异常时返回空集合并上报异常
    /// </summary>
    [Fact]
    public void GetKeys_ForNonStringKeyType_ReturnsEmptyWhenErrorsHidden()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.Create<SampleCacheItem, int>();

        Assert.Empty(cache.GetKeys(hideErrors: true));
        Assert.Single(context.Notifier.Exceptions);
    }

    /// <summary>
    /// 后端支持键模式时按模式移除直接下推，并原样返回移除数量
    /// </summary>
    [Fact]
    public void RemoveByPattern_WithPatternCapableBackend_DelegatesToBackend()
    {
        var store = new FakeCapableDistributedCacheStore { RemoveByPatternResult = 3 };
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var removed = cache.RemoveByPattern("user:*");

        Assert.Equal(3L, removed);
        Assert.Equal(1, store.RemoveByPatternCount);
        Assert.Equal(SamplePrefix + "user:*", store.LastPattern);
    }

    /// <summary>
    /// 异步按模式移除同样直接下推
    /// </summary>
    [Fact]
    public async Task RemoveByPatternAsync_WithPatternCapableBackend_DelegatesToBackend()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeCapableDistributedCacheStore { RemoveByPatternResult = 5 };
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var removed = await cache.RemoveByPatternAsync("user:*", token: token);

        Assert.Equal(5L, removed);
        Assert.Equal(SamplePrefix + "user:*", store.LastPattern);
    }

    /// <summary>
    /// 后端不支持键模式时按模式移除取不到键，返回零
    /// </summary>
    [Fact]
    public void RemoveByPattern_WithBackendLackingPatternSupport_ReturnsZero()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        cache.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Equal(0L, cache.RemoveByPattern());
        // 取不到键就不该顺手删掉已有数据
        Assert.Equal("v1", cache.Get("k1")?.Value);
    }

    /// <summary>
    /// 空脚本被拒绝，未隐藏异常时抛出参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ScriptEvaluate_WithBlankScript_ThrowsWhenErrorsNotHidden(string script)
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var exception = Assert.Throws<ArgumentException>(() => cache.ScriptEvaluate(script, hideErrors: false));

        Assert.Equal("script", exception.ParamName);
    }

    /// <summary>
    /// 空脚本在隐藏异常时返回空结果并上报异常
    /// </summary>
    [Fact]
    public void ScriptEvaluate_WithBlankScript_ReturnsNullWhenErrorsHidden()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Null(cache.ScriptEvaluate(string.Empty, hideErrors: true));
        Assert.Single(context.Notifier.Exceptions);
    }

    /// <summary>
    /// 后端不具备脚本能力时抛出不支持异常
    /// </summary>
    [Fact]
    public void ScriptEvaluate_WithBackendLackingScriptSupport_Throws()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Throws<NotSupportedException>(() => cache.ScriptEvaluate("return 1", hideErrors: false));
    }

    /// <summary>
    /// 脚本的键被规范化，参数原样透传，结果原样返回
    /// </summary>
    [Fact]
    public void ScriptEvaluate_NormalizesKeysAndForwardsValues()
    {
        var store = new FakeCapableDistributedCacheStore
        {
            ScriptResult = CacheScriptResult.FromValue(7L)
        };
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var result = cache.ScriptEvaluate("return 1", ["k1", "k2"], ["a", 2]);

        Assert.Equal(7L, result?.AsInt64());
        Assert.Equal("return 1", store.LastScript);
        Assert.Equal(new[] { SamplePrefix + "k1", SamplePrefix + "k2" }, store.LastScriptKeys);
        Assert.Equal(["a", 2], store.LastScriptValues);
    }

    /// <summary>
    /// 不给键与参数时下推空数组而不是空引用
    /// </summary>
    [Fact]
    public async Task ScriptEvaluateAsync_WithoutKeysAndValues_PassesEmptyArrays()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        await cache.ScriptEvaluateAsync("return 1", token: token);

        Assert.NotNull(store.LastScriptKeys);
        Assert.Empty(store.LastScriptKeys);
        Assert.NotNull(store.LastScriptValues);
        Assert.Empty(store.LastScriptValues);
    }
}
