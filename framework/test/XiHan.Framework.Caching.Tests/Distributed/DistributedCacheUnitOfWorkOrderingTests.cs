// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Caching.Tests.Fakes;
using XiHan.Framework.Threading;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Caching.Tests.Distributed;

/// <summary>
/// 工作单元参与批量读取时的结果对齐测试
/// </summary>
/// <remarks>
/// 批量接口的对外约定是「返回项与入参键数量相同且按位对应」。considerUow=true 时，
/// 命中工作单元缓存的项与从后端读回的项来自两次独立收集，按「先命中、后未命中」直接拼接就会偏离入参顺序；
/// GetOrAddMany 随后又按下标反查缺失键，错位会让工厂拿到已命中的键，并把工厂结果写进别的键的槽位，
/// 最终某个入参键整个从结果里消失。工作单元分支在其它用例里没有被驱动过，这里用可覆写的
/// ShouldConsiderUow / GetUnitOfWorkCache 精确构造「部分命中」场景，把顺序与工厂入参逐条锁死。
/// </remarks>
public class DistributedCacheUnitOfWorkOrderingTests
{
    /// <summary>
    /// 工作单元部分命中时，多键读取结果仍与入参键同序
    /// </summary>
    [Fact]
    public void GetMany_WhenUnitOfWorkPartiallyHits_KeepsRequestedKeyOrder()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = CreateCache(context);
        cache.Set("b", new SampleCacheItem { Value = "B" });
        cache.UnitOfWorkCache["c"] = new UnitOfWorkCacheItem<SampleCacheItem>(new SampleCacheItem { Value = "C" });

        var result = cache.GetMany(["a", "b", "c"], considerUow: true);

        Assert.Equal(["a", "b", "c"], result.Select(pair => pair.Key));
        Assert.Null(result[0].Value);
        Assert.Equal("B", result[1].Value?.Value);
        Assert.Equal("C", result[2].Value?.Value);
    }

    /// <summary>
    /// 异步多键读取在工作单元部分命中时保持同样的对齐语义
    /// </summary>
    [Fact]
    public async Task GetManyAsync_WhenUnitOfWorkPartiallyHits_KeepsRequestedKeyOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = CreateCache(context);
        await cache.SetAsync("b", new SampleCacheItem { Value = "B" }, token: token);
        cache.UnitOfWorkCache["c"] = new UnitOfWorkCacheItem<SampleCacheItem>(new SampleCacheItem { Value = "C" });

        var result = await cache.GetManyAsync(["a", "b", "c"], considerUow: true, token: token);

        Assert.Equal(["a", "b", "c"], result.Select(pair => pair.Key));
        Assert.Null(result[0].Value);
        Assert.Equal("B", result[1].Value?.Value);
        Assert.Equal("C", result[2].Value?.Value);
    }

    /// <summary>
    /// 工作单元部分命中时，只有真正缺失的键被交给工厂，且工厂结果落在该键自己的位置上
    /// </summary>
    /// <remarks>
    /// 这是错位造成实质数据错误的场景：命中项排在最前面，缺失位落在了本已命中的键的下标上，
    /// 工厂被问了一个不缺的键，返回值又被写进另一个键的槽位，"a" 则从结果里彻底消失。
    /// </remarks>
    [Fact]
    public void GetOrAddMany_WhenUnitOfWorkPartiallyHits_AsksFactoryForTheRealMissingKeyOnly()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = CreateCache(context);
        cache.Set("b", new SampleCacheItem { Value = "B" });
        cache.UnitOfWorkCache["c"] = new UnitOfWorkCacheItem<SampleCacheItem>(new SampleCacheItem { Value = "C" });
        var requested = new List<string>();

        var result = cache.GetOrAddMany(["a", "b", "c"], keys =>
        {
            requested.AddRange(keys);

            return [.. keys.Select(key => new KeyValuePair<string, SampleCacheItem>(key, new SampleCacheItem { Value = "built-" + key }))];
        }, considerUow: true);

        Assert.Equal(new[] { "a" }, requested);
        Assert.Equal(["a", "b", "c"], result.Select(pair => pair.Key));
        Assert.Equal("built-a", result[0].Value?.Value);
        Assert.Equal("B", result[1].Value?.Value);
        Assert.Equal("C", result[2].Value?.Value);
        Assert.Equal("built-a", cache.UnitOfWorkCache["a"].Value?.Value);
    }

    /// <summary>
    /// 异步批量获取或添加在工作单元部分命中时同样只补真正缺失的键
    /// </summary>
    [Fact]
    public async Task GetOrAddManyAsync_WhenUnitOfWorkPartiallyHits_AsksFactoryForTheRealMissingKeyOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = CreateCache(context);
        await cache.SetAsync("b", new SampleCacheItem { Value = "B" }, token: token);
        cache.UnitOfWorkCache["c"] = new UnitOfWorkCacheItem<SampleCacheItem>(new SampleCacheItem { Value = "C" });
        var requested = new List<string>();

        var result = await cache.GetOrAddManyAsync(["a", "b", "c"], keys =>
        {
            requested.AddRange(keys);

            return Task.FromResult(keys
                .Select(key => new KeyValuePair<string, SampleCacheItem>(key, new SampleCacheItem { Value = "built-" + key }))
                .ToList());
        }, considerUow: true, token: token);

        Assert.Equal(new[] { "a" }, requested);
        Assert.Equal(["a", "b", "c"], result.Select(pair => pair.Key));
        Assert.Equal("built-a", result[0].Value?.Value);
        Assert.Equal("B", result[1].Value?.Value);
        Assert.Equal("C", result[2].Value?.Value);
    }

    /// <summary>
    /// 工作单元命中全部键时不访问后端，也不调用工厂，结果按入参顺序返回
    /// </summary>
    /// <remarks>
    /// 这条走的是「全部命中直接返回」的提前退出分支，用倒序键确认它同样按入参顺序而不是按内部收集顺序。
    /// </remarks>
    [Fact]
    public void GetOrAddMany_WhenUnitOfWorkHitsEveryKey_SkipsBackendAndFactory()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = CreateCache(context);
        cache.UnitOfWorkCache["a"] = new UnitOfWorkCacheItem<SampleCacheItem>(new SampleCacheItem { Value = "A" });
        cache.UnitOfWorkCache["b"] = new UnitOfWorkCacheItem<SampleCacheItem>(new SampleCacheItem { Value = "B" });
        var invoked = false;

        var result = cache.GetOrAddMany(["b", "a"], keys =>
        {
            invoked = true;

            return [];
        }, considerUow: true);

        Assert.False(invoked);
        Assert.Equal(0, store.GetManyCount);
        Assert.Equal(["b", "a"], result.Select(pair => pair.Key));
        Assert.Equal("B", result[0].Value?.Value);
        Assert.Equal("A", result[1].Value?.Value);
    }

    /// <summary>
    /// 入参含重复键时，结果按位置逐个还原，不因内部去重而缩短或错位
    /// </summary>
    [Fact]
    public void GetMany_WithDuplicatedKeysUnderUnitOfWork_ReturnsOneEntryPerRequestedPosition()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = CreateCache(context);
        cache.Set("b", new SampleCacheItem { Value = "B" });
        cache.UnitOfWorkCache["a"] = new UnitOfWorkCacheItem<SampleCacheItem>(new SampleCacheItem { Value = "A" });

        var result = cache.GetMany(["a", "b", "a"], considerUow: true);

        Assert.Equal(["a", "b", "a"], result.Select(pair => pair.Key));
        Assert.Equal("A", result[0].Value?.Value);
        Assert.Equal("B", result[1].Value?.Value);
        Assert.Equal("A", result[2].Value?.Value);
    }

    /// <summary>
    /// 不启用工作单元时完全不看工作单元缓存，结果只反映后端内容
    /// </summary>
    /// <remarks>
    /// 反例：确认对齐修复没有把工作单元缓存顺带引入 considerUow=false 的直连路径。
    /// </remarks>
    [Fact]
    public void GetMany_WithoutConsiderUow_IgnoresUnitOfWorkCache()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = CreateCache(context);
        cache.Set("b", new SampleCacheItem { Value = "B" });
        cache.UnitOfWorkCache["c"] = new UnitOfWorkCacheItem<SampleCacheItem>(new SampleCacheItem { Value = "C" });

        var result = cache.GetMany(["a", "b", "c"]);

        Assert.Equal(["a", "b", "c"], result.Select(pair => pair.Key));
        Assert.Null(result[0].Value);
        Assert.Equal("B", result[1].Value?.Value);
        Assert.Null(result[2].Value);
    }

    /// <summary>
    /// 用测试上下文的协作者组装出可摆布工作单元缓存的分布式缓存
    /// </summary>
    /// <param name="context">分布式缓存测试上下文</param>
    /// <returns>分布式缓存</returns>
    private static UnitOfWorkAwareDistributedCache CreateCache(DistributedCacheTestContext context)
    {
        return new UnitOfWorkAwareDistributedCache(
            Microsoft.Extensions.Options.Options.Create(context.Options),
            context.Inner,
            NullCancellationTokenProvider.Instance,
            context.Serializer,
            context.KeyNormalizer,
            context.Provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeUnitOfWorkManager());
    }

    /// <summary>
    /// 把工作单元缓存换成可直接预置的字典的分布式缓存
    /// </summary>
    /// <remarks>
    /// 真实工作单元基础设施对缓存用例过重，而 considerUow 分支只依赖 ShouldConsiderUow 与 GetUnitOfWorkCache
    /// 这两个可覆写点，覆写它们就能稳定构造出「部分键命中工作单元缓存」的场景。
    /// </remarks>
    private sealed class UnitOfWorkAwareDistributedCache : DistributedCache<SampleCacheItem, string>
    {
        private readonly Dictionary<string, UnitOfWorkCacheItem<SampleCacheItem>> _unitOfWorkCache = new(StringComparer.Ordinal);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="distributedCacheOption">分布式缓存选项</param>
        /// <param name="cache">缓存后端</param>
        /// <param name="cancellationTokenProvider">取消令牌提供程序</param>
        /// <param name="serializer">序列化器</param>
        /// <param name="keyNormalizer">键规范化器</param>
        /// <param name="serviceScopeFactory">服务作用域工厂</param>
        /// <param name="unitOfWorkManager">工作单元管理器</param>
        public UnitOfWorkAwareDistributedCache(
            Microsoft.Extensions.Options.IOptions<XiHanDistributedCacheOptions> distributedCacheOption,
            IDistributedCache cache,
            ICancellationTokenProvider cancellationTokenProvider,
            IDistributedCacheSerializer serializer,
            IDistributedCacheKeyNormalizer keyNormalizer,
            IServiceScopeFactory serviceScopeFactory,
            IUnitOfWorkManager unitOfWorkManager)
            : base(distributedCacheOption, cache, cancellationTokenProvider, serializer, keyNormalizer, serviceScopeFactory, unitOfWorkManager)
        {
        }

        /// <summary>
        /// 工作单元缓存，用例可直接预置命中项
        /// </summary>
        public Dictionary<string, UnitOfWorkCacheItem<SampleCacheItem>> UnitOfWorkCache => _unitOfWorkCache;

        /// <summary>
        /// 只按调用方意愿判断，不要求存在真实工作单元
        /// </summary>
        /// <param name="considerUow">调用方是否要求考虑工作单元</param>
        /// <returns>是否考虑工作单元</returns>
        protected override bool ShouldConsiderUow(bool considerUow)
        {
            return considerUow;
        }

        /// <summary>
        /// 返回用例可直接预置的工作单元缓存
        /// </summary>
        /// <returns>工作单元缓存</returns>
        protected override Dictionary<string, UnitOfWorkCacheItem<SampleCacheItem>> GetUnitOfWorkCache()
        {
            return _unitOfWorkCache;
        }
    }
}
