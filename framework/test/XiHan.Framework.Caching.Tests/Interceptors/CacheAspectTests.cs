// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Caching.Interceptors;

namespace XiHan.Framework.Caching.Tests.Interceptors;

/// <summary>
/// 缓存切面测试
/// </summary>
/// <remarks>
/// 切面被动态代理与 Web 层过滤器共用，静态解析方法决定两条入口的口径是否一致；
/// 读写部分用真实 HybridCache（仅进程内一级缓存）驱动，不接分布式后端。
/// </remarks>
public class CacheAspectTests
{
    /// <summary>
    /// 标注了可缓存的方法能解析出特性
    /// </summary>
    [Fact]
    public void GetCacheableAttributeOrNull_ForMarkedMethod_ReturnsAttribute()
    {
        var attribute = CacheAspect.GetCacheableAttributeOrNull(GetMethod(nameof(Target.GetNameAsync)));

        Assert.NotNull(attribute);
        Assert.Equal("cfg:{id}", attribute.Key);
    }

    /// <summary>
    /// 未标注的方法解析结果为空
    /// </summary>
    [Fact]
    public void GetCacheableAttributeOrNull_ForPlainMethod_ReturnsNull()
    {
        Assert.Null(CacheAspect.GetCacheableAttributeOrNull(GetMethod(nameof(Target.PlainAsync))));
    }

    /// <summary>
    /// 同一方法的解析结果被缓存复用
    /// </summary>
    /// <remarks>
    /// 特性解析在每次方法调用上都会发生，必须按 MethodInfo 缓存，否则热路径上会反复走反射。
    /// </remarks>
    [Fact]
    public void GetCacheableAttributeOrNull_CalledTwice_ReturnsSameInstance()
    {
        var method = GetMethod(nameof(Target.GetNameAsync));

        Assert.Same(
            CacheAspect.GetCacheableAttributeOrNull(method),
            CacheAspect.GetCacheableAttributeOrNull(method));
    }

    /// <summary>
    /// 方法为空时拒绝解析可缓存特性
    /// </summary>
    [Fact]
    public void GetCacheableAttributeOrNull_WithNullMethod_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CacheAspect.GetCacheableAttributeOrNull(null!));
    }

    /// <summary>
    /// 多个清除标注全部被解析出来
    /// </summary>
    [Fact]
    public void GetCacheEvictAttributes_ForMarkedMethod_ReturnsAll()
    {
        var attributes = CacheAspect.GetCacheEvictAttributes(GetMethod(nameof(Target.UpdateManyAsync)));

        Assert.Equal(2, attributes.Length);
    }

    /// <summary>
    /// 未标注清除声明的方法解析出空数组
    /// </summary>
    [Fact]
    public void GetCacheEvictAttributes_ForPlainMethod_ReturnsEmpty()
    {
        Assert.Empty(CacheAspect.GetCacheEvictAttributes(GetMethod(nameof(Target.PlainAsync))));
    }

    /// <summary>
    /// 方法为空时拒绝解析清除特性
    /// </summary>
    [Fact]
    public void GetCacheEvictAttributes_WithNullMethod_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CacheAspect.GetCacheEvictAttributes(null!));
    }

    /// <summary>
    /// 异步方法的可缓存值类型取自 Task 的类型实参
    /// </summary>
    [Fact]
    public void GetCacheableValueTypeOrNull_ForGenericTask_ReturnsTypeArgument()
    {
        Assert.Equal(typeof(string), CacheAspect.GetCacheableValueTypeOrNull(GetMethod(nameof(Target.GetNameAsync))));
    }

    /// <summary>
    /// 同步方法的可缓存值类型就是返回类型
    /// </summary>
    [Fact]
    public void GetCacheableValueTypeOrNull_ForSyncMethod_ReturnsReturnType()
    {
        Assert.Equal(typeof(int), CacheAspect.GetCacheableValueTypeOrNull(GetMethod(nameof(Target.GetCount))));
    }

    /// <summary>
    /// 无返回值的方法没有可缓存值类型
    /// </summary>
    [Theory]
    [InlineData(nameof(Target.UpdateManyAsync))]
    [InlineData(nameof(Target.DoNothing))]
    public void GetCacheableValueTypeOrNull_ForVoidLikeMethod_ReturnsNull(string methodName)
    {
        Assert.Null(CacheAspect.GetCacheableValueTypeOrNull(GetMethod(methodName)));
    }

    /// <summary>
    /// 方法为空时拒绝解析可缓存值类型
    /// </summary>
    [Fact]
    public void GetCacheableValueTypeOrNull_WithNullMethod_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CacheAspect.GetCacheableValueTypeOrNull(null!));
    }

    /// <summary>
    /// 首次取值执行工厂并写入缓存，再次取值直接命中
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_SecondCall_HitsCacheWithoutInvokingFactory()
    {
        using var provider = BuildProvider();
        var aspect = new CacheAspect(provider.GetRequiredService<HybridCache>());
        var cacheKey = NewKey();
        var calls = 0;

        var first = await aspect.GetOrCreateAsync(typeof(string), cacheKey, 60, () =>
        {
            calls++;
            return Task.FromResult<object?>("v1");
        });
        var second = await aspect.GetOrCreateAsync(typeof(string), cacheKey, 60, () =>
        {
            calls++;
            return Task.FromResult<object?>("v2");
        });

        Assert.Equal("v1", first);
        Assert.Equal("v1", second);
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// 不同缓存键各自取值
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_ForDifferentKeys_KeepsValuesSeparate()
    {
        using var provider = BuildProvider();
        var aspect = new CacheAspect(provider.GetRequiredService<HybridCache>());
        var prefix = NewKey();

        var first = await aspect.GetOrCreateAsync(typeof(string), prefix + ":1", 60, () => Task.FromResult<object?>("a"));
        var second = await aspect.GetOrCreateAsync(typeof(string), prefix + ":2", 60, () => Task.FromResult<object?>("b"));

        Assert.Equal("a", first);
        Assert.Equal("b", second);
    }

    /// <summary>
    /// 清除后再次取值重新执行工厂
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EvictAsync_RemovesCachedValue()
    {
        using var provider = BuildProvider();
        var aspect = new CacheAspect(provider.GetRequiredService<HybridCache>());
        var method = GetMethod(nameof(Target.UpdateAsync));
        var attributes = CacheAspect.GetCacheEvictAttributes(method);
        var calls = 0;

        Task<object?> Factory()
        {
            calls++;
            return Task.FromResult<object?>($"v{calls}");
        }

        var before = await aspect.GetOrCreateAsync(typeof(string), "cfg:1", 60, Factory);
        await aspect.EvictAsync(method, [1], attributes);
        var after = await aspect.GetOrCreateAsync(typeof(string), "cfg:1", 60, Factory);

        Assert.Equal("v1", before);
        Assert.Equal("v2", after);
        Assert.Equal(2, calls);
    }

    /// <summary>
    /// 清除特性集合为空时拒绝执行
    /// </summary>
    [Fact]
    public async Task EvictAsync_WithNullAttributes_Throws()
    {
        using var provider = BuildProvider();
        var aspect = new CacheAspect(provider.GetRequiredService<HybridCache>());
        var method = GetMethod(nameof(Target.UpdateAsync));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => aspect.EvictAsync(method, [1], null!));
    }

    /// <summary>
    /// 取值参数非法时拒绝执行
    /// </summary>
    [Fact]
    public void GetOrCreateAsync_WithInvalidArguments_Throws()
    {
        using var provider = BuildProvider();
        var aspect = new CacheAspect(provider.GetRequiredService<HybridCache>());

        // 参数校验发生在进入缓存之前，所以用同步断言；这里刻意丢弃返回的任务，避免绑定到异步重载
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = aspect.GetOrCreateAsync(null!, "k", 60, () => Task.FromResult<object?>("v"));
        });
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            _ = aspect.GetOrCreateAsync(typeof(string), "   ", 60, () => Task.FromResult<object?>("v"));
        });
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = aspect.GetOrCreateAsync(typeof(string), "k", 60, null!);
        });
    }

    /// <summary>
    /// 构建只含混合缓存的容器
    /// </summary>
    /// <returns>服务提供者</returns>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 生成互不冲突的缓存键
    /// </summary>
    /// <returns>缓存键</returns>
    private static string NewKey()
    {
        return "aspect:" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 取目标类型上的方法
    /// </summary>
    /// <param name="name">方法名</param>
    /// <returns>方法信息</returns>
    private static MethodInfo GetMethod(string name)
    {
        return typeof(Target).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;
    }

    /// <summary>
    /// 承载缓存标注的目标类型
    /// </summary>
    private sealed class Target
    {
        /// <summary>
        /// 标注了可缓存的异步方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>名称</returns>
        [Cacheable(Key = "cfg:{id}", ExpireSeconds = 60)]
        public Task<string> GetNameAsync(int id)
        {
            return Task.FromResult($"n{id}");
        }

        /// <summary>
        /// 标注了可缓存的同步方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>数量</returns>
        [Cacheable(Key = "count:{id}")]
        public int GetCount(int id)
        {
            return id;
        }

        /// <summary>
        /// 标注了单个清除声明的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>异步任务</returns>
        [CacheEvict(Key = "cfg:{id}")]
        public Task UpdateAsync(int id)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 标注了多个清除声明的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>异步任务</returns>
        [CacheEvict(Key = "cfg:{id}")]
        [CacheEvict(Key = "count:{id}")]
        public Task UpdateManyAsync(int id)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 未标注任何缓存声明的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>名称</returns>
        public Task<string> PlainAsync(int id)
        {
            return Task.FromResult($"p{id}");
        }

        /// <summary>
        /// 无返回值的方法
        /// </summary>
        public void DoNothing()
        {
        }
    }
}
