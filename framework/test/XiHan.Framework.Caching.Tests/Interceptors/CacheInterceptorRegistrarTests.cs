// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Caching.Interceptors;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 缓存拦截器注册器测试
/// </summary>
/// <remarks>
/// 注册器决定哪些服务会被套上动态代理。挂多了会给无缓存需求的服务平白加一层代理开销，
/// 挂少了则标注形同虚设，所以「有标注才挂、只看自身声明的方法、重复注册不叠加」三条都要覆盖。
/// </remarks>
public class CacheInterceptorRegistrarTests
{
    /// <summary>
    /// 自身声明了可缓存方法的类型会被挂上缓存拦截器
    /// </summary>
    [Fact]
    public void RegisterIfNeeded_ForTypeWithCacheableMethod_AddsInterceptor()
    {
        var context = new FakeServiceRegistredContext(typeof(CacheableService));

        CacheInterceptorRegistrar.RegisterIfNeeded(context);

        Assert.Contains(typeof(CacheInterceptor), context.Interceptors);
    }

    /// <summary>
    /// 自身声明了清除方法的类型同样会被挂上缓存拦截器
    /// </summary>
    [Fact]
    public void RegisterIfNeeded_ForTypeWithEvictMethod_AddsInterceptor()
    {
        var context = new FakeServiceRegistredContext(typeof(EvictingService));

        CacheInterceptorRegistrar.RegisterIfNeeded(context);

        Assert.Contains(typeof(CacheInterceptor), context.Interceptors);
    }

    /// <summary>
    /// 没有任何缓存标注的类型不会被挂拦截器
    /// </summary>
    [Fact]
    public void RegisterIfNeeded_ForPlainType_AddsNothing()
    {
        var context = new FakeServiceRegistredContext(typeof(PlainService));

        CacheInterceptorRegistrar.RegisterIfNeeded(context);

        Assert.Empty(context.Interceptors);
    }

    /// <summary>
    /// 只继承而未重新声明缓存方法的派生类型不会被挂拦截器
    /// </summary>
    /// <remarks>
    /// 判定只看类型自身声明的公开实例方法，纯继承的派生类型由基类那一侧负责挂代理。
    /// </remarks>
    [Fact]
    public void RegisterIfNeeded_ForDerivedTypeWithoutOwnDeclaration_AddsNothing()
    {
        var context = new FakeServiceRegistredContext(typeof(DerivedFromCacheableService));

        CacheInterceptorRegistrar.RegisterIfNeeded(context);

        Assert.Empty(context.Interceptors);
    }

    /// <summary>
    /// 重复注册不会把同一个拦截器挂两次
    /// </summary>
    [Fact]
    public void RegisterIfNeeded_CalledTwice_AddsInterceptorOnce()
    {
        var context = new FakeServiceRegistredContext(typeof(CacheableService));

        CacheInterceptorRegistrar.RegisterIfNeeded(context);
        CacheInterceptorRegistrar.RegisterIfNeeded(context);

        Assert.Single(context.Interceptors);
    }

    /// <summary>
    /// 自身声明了可缓存方法的服务
    /// </summary>
    private class CacheableService
    {
        /// <summary>
        /// 可缓存的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>名称</returns>
        [Cacheable(Key = "registrar:{id}")]
        public virtual Task<string> GetNameAsync(int id)
        {
            return Task.FromResult($"n{id}");
        }
    }

    /// <summary>
    /// 只继承而未重新声明任何方法的派生服务
    /// </summary>
    private sealed class DerivedFromCacheableService : CacheableService;

    /// <summary>
    /// 自身声明了清除方法的服务
    /// </summary>
    private sealed class EvictingService
    {
        /// <summary>
        /// 触发缓存清除的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>异步任务</returns>
        [CacheEvict(Key = "registrar:{id}")]
        public Task UpdateAsync(int id)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 没有任何缓存标注的服务
    /// </summary>
    private sealed class PlainService
    {
        /// <summary>
        /// 普通方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>名称</returns>
        public Task<string> GetNameAsync(int id)
        {
            return Task.FromResult($"p{id}");
        }
    }
}
