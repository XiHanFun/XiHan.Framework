// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Caching.Interceptors;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 缓存拦截器测试
/// </summary>
/// <remarks>
/// 用方法调用替身模拟代理链：命中缓存时目标方法必须一次都不执行，这是 AOP 缓存唯一的价值来源；
/// 清除声明必须在目标方法执行之后才生效，否则并发下会把旧值重新灌回缓存。
/// </remarks>
public class CacheInterceptorTests
{
    /// <summary>
    /// 首次调用穿透到目标方法，第二次同参调用直接命中缓存
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InterceptAsync_ForCacheableMethod_SkipsTargetOnSecondCall()
    {
        using var provider = BuildProvider();
        var interceptor = CreateInterceptor(provider);
        var method = GetMethod(nameof(Target.GetNameAsync));

        var first = new FakeMethodInvocation(method, [1], () => Task.FromResult("n1"));
        await interceptor.InterceptAsync(first);

        var second = new FakeMethodInvocation(method, [1], () => Task.FromResult("changed"));
        await interceptor.InterceptAsync(second);

        Assert.Equal("n1", first.ReturnValue);
        Assert.Equal("n1", second.ReturnValue);
        Assert.Equal(1, first.ProceedCount);
        Assert.Equal(0, second.ProceedCount);
    }

    /// <summary>
    /// 不同实参落到不同缓存键，互不覆盖
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InterceptAsync_ForCacheableMethod_KeysByArguments()
    {
        using var provider = BuildProvider();
        var interceptor = CreateInterceptor(provider);
        var method = GetMethod(nameof(Target.GetNameAsync));

        var first = new FakeMethodInvocation(method, [1], () => Task.FromResult("n1"));
        var second = new FakeMethodInvocation(method, [2], () => Task.FromResult("n2"));
        await interceptor.InterceptAsync(first);
        await interceptor.InterceptAsync(second);

        Assert.Equal("n1", first.ReturnValue);
        Assert.Equal("n2", second.ReturnValue);
        Assert.Equal(1, second.ProceedCount);
    }

    /// <summary>
    /// 同步返回值的可缓存方法同样被缓存
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InterceptAsync_ForSynchronousCacheableMethod_CachesReturnValue()
    {
        using var provider = BuildProvider();
        var interceptor = CreateInterceptor(provider);
        var method = GetMethod(nameof(Target.GetLabel));

        var first = new FakeMethodInvocation(method, [1], () => "s1");
        await interceptor.InterceptAsync(first);

        var second = new FakeMethodInvocation(method, [1], () => "changed");
        await interceptor.InterceptAsync(second);

        Assert.Equal("s1", first.ReturnValue);
        Assert.Equal("s1", second.ReturnValue);
        Assert.Equal(0, second.ProceedCount);
    }

    /// <summary>
    /// 无返回值的可缓存方法不做缓存，每次都执行目标方法
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InterceptAsync_ForCacheableMethodWithoutResult_AlwaysProceeds()
    {
        using var provider = BuildProvider();
        var interceptor = CreateInterceptor(provider);
        var method = GetMethod(nameof(Target.TouchAsync));

        var first = new FakeMethodInvocation(method, [1], () => Task.CompletedTask);
        var second = new FakeMethodInvocation(method, [1], () => Task.CompletedTask);
        await interceptor.InterceptAsync(first);
        await interceptor.InterceptAsync(second);

        Assert.Equal(1, first.ProceedCount);
        Assert.Equal(1, second.ProceedCount);
    }

    /// <summary>
    /// 未标注任何缓存声明的方法只是原样放行
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InterceptAsync_ForPlainMethod_JustProceeds()
    {
        using var provider = BuildProvider();
        var interceptor = CreateInterceptor(provider);
        var method = GetMethod(nameof(Target.PlainAsync));

        var invocation = new FakeMethodInvocation(method, [1], () => Task.FromResult("p1"));
        await interceptor.InterceptAsync(invocation);

        Assert.Equal(1, invocation.ProceedCount);
        Assert.NotNull(invocation.ReturnValue);
    }

    /// <summary>
    /// 标注了清除声明的方法先执行目标方法，再让对应缓存键失效
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InterceptAsync_ForEvictMethod_ProceedsThenInvalidatesCache()
    {
        using var provider = BuildProvider();
        var interceptor = CreateInterceptor(provider);
        var getMethod = GetMethod(nameof(Target.GetNameAsync));

        var primed = new FakeMethodInvocation(getMethod, [1], () => Task.FromResult("old"));
        await interceptor.InterceptAsync(primed);

        var update = new FakeMethodInvocation(GetMethod(nameof(Target.UpdateAsync)), [1], () => Task.CompletedTask);
        await interceptor.InterceptAsync(update);

        var reloaded = new FakeMethodInvocation(getMethod, [1], () => Task.FromResult("new"));
        await interceptor.InterceptAsync(reloaded);

        Assert.Equal(1, update.ProceedCount);
        Assert.Equal("new", reloaded.ReturnValue);
        Assert.Equal(1, reloaded.ProceedCount);
    }

    /// <summary>
    /// 清除声明只作用于自己的键，不牵连其他键
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InterceptAsync_ForEvictMethod_LeavesOtherKeysIntact()
    {
        using var provider = BuildProvider();
        var interceptor = CreateInterceptor(provider);
        var getMethod = GetMethod(nameof(Target.GetNameAsync));

        await interceptor.InterceptAsync(new FakeMethodInvocation(getMethod, [1], () => Task.FromResult("one")));
        await interceptor.InterceptAsync(new FakeMethodInvocation(getMethod, [2], () => Task.FromResult("two")));

        var update = new FakeMethodInvocation(GetMethod(nameof(Target.UpdateAsync)), [1], () => Task.CompletedTask);
        await interceptor.InterceptAsync(update);

        var reloadedOther = new FakeMethodInvocation(getMethod, [2], () => Task.FromResult("changed"));
        await interceptor.InterceptAsync(reloadedOther);

        Assert.Equal("two", reloadedOther.ReturnValue);
        Assert.Equal(0, reloadedOther.ProceedCount);
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
    /// 基于容器创建拦截器
    /// </summary>
    /// <param name="provider">服务提供者</param>
    /// <returns>缓存拦截器</returns>
    private static CacheInterceptor CreateInterceptor(ServiceProvider provider)
    {
        return new CacheInterceptor(new CacheAspect(provider.GetRequiredService<HybridCache>()));
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
    /// 被拦截的目标类型
    /// </summary>
    private sealed class Target
    {
        /// <summary>
        /// 可缓存的异步方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>名称</returns>
        [Cacheable(Key = "interceptor:name:{id}", ExpireSeconds = 120)]
        public Task<string> GetNameAsync(int id)
        {
            return Task.FromResult($"n{id}");
        }

        /// <summary>
        /// 可缓存的同步方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>标签</returns>
        [Cacheable(Key = "interceptor:label:{id}")]
        public string GetLabel(int id)
        {
            return $"s{id}";
        }

        /// <summary>
        /// 标注了可缓存但没有返回值的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>异步任务</returns>
        [Cacheable(Key = "interceptor:touch:{id}")]
        public Task TouchAsync(int id)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 让名称缓存失效的方法
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns>异步任务</returns>
        [CacheEvict(Key = "interceptor:name:{id}")]
        public Task UpdateAsync(int id)
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
    }
}
