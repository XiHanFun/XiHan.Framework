// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 缓存服务提供器测试
/// </summary>
/// <remarks>
/// 缓存服务提供器的全部价值在于「同一个提供器实例内，任何服务只解析一次」，
/// 瞬时服务也被缓存正是它与原生容器的区别；同时它按服务标识区分键控与非键控条目。
/// 这里还顺带验证其自身经约定注册后的生命周期（作用域 / 瞬时）确实生效。
/// </remarks>
public class CachedServiceProviderTests
{
    /// <summary>
    /// 瞬时服务在同一缓存提供器内只解析一次
    /// </summary>
    [Fact]
    public void GetService_WhenTransientService_CachesFirstResolution()
    {
        var counter = new CspInstanceCounter();
        using var provider = BuildProvider(counter);
        var cached = new CachedServiceProvider(provider);

        var first = cached.GetService(typeof(CspTransientService));
        var second = cached.GetService(typeof(CspTransientService));

        Assert.Same(first, second);
        Assert.Equal(1, counter.Created);
    }

    /// <summary>
    /// 原生容器解析瞬时服务每次都新建，与缓存行为形成对照
    /// </summary>
    [Fact]
    public void GetService_WhenResolvedFromRawProvider_CreatesNewInstanceEachTime()
    {
        var counter = new CspInstanceCounter();
        using var provider = BuildProvider(counter);

        provider.GetRequiredService<CspTransientService>();
        provider.GetRequiredService<CspTransientService>();

        Assert.Equal(2, counter.Created);
    }

    /// <summary>
    /// 构造时预置服务提供器自身
    /// </summary>
    [Fact]
    public void GetService_WhenServiceProviderRequested_ReturnsUnderlyingProvider()
    {
        var counter = new CspInstanceCounter();
        using var provider = BuildProvider(counter);
        var cached = new CachedServiceProvider(provider);

        Assert.Same(provider, cached.GetService(typeof(IServiceProvider)));
    }

    /// <summary>
    /// 未注册服务时返回给定的默认值
    /// </summary>
    [Fact]
    public void GetService_WhenNotRegistered_ReturnsDefaultValue()
    {
        var counter = new CspInstanceCounter();
        using var provider = BuildProvider(counter);
        var cached = new CachedServiceProvider(provider);
        var fallback = new CspFallback();

        Assert.Same(fallback, cached.GetService(fallback));
        Assert.Same(fallback, cached.GetService(typeof(CspFallback), fallback));
    }

    /// <summary>
    /// 使用工厂兜底时工厂只执行一次并被缓存
    /// </summary>
    [Fact]
    public void GetService_WhenFactoryGiven_InvokesFactoryOnceAndCaches()
    {
        var counter = new CspInstanceCounter();
        using var provider = BuildProvider(counter);
        var cached = new CachedServiceProvider(provider);
        var factoryCalls = 0;
        Func<IServiceProvider, object> factory = _ =>
        {
            factoryCalls++;
            return new CspFallback();
        };

        var first = cached.GetService<CspFallback>(factory);
        var second = cached.GetService<CspFallback>(factory);

        Assert.Same(first, second);
        Assert.Equal(1, factoryCalls);
    }

    /// <summary>
    /// 键控服务同样被缓存且与非键控条目互不干扰
    /// </summary>
    [Fact]
    public void GetKeyedService_CachesPerKeyAndKeepsPlainEntrySeparate()
    {
        var counter = new CspInstanceCounter();
        using var provider = BuildProvider(counter);
        var cached = new CachedServiceProvider(provider);

        var keyedFirst = cached.GetKeyedService(typeof(CspTransientService), "alpha");
        var keyedSecond = cached.GetKeyedService(typeof(CspTransientService), "alpha");
        var plain = cached.GetService(typeof(CspTransientService));

        Assert.Same(keyedFirst, keyedSecond);
        Assert.NotSame(keyedFirst, plain);
    }

    /// <summary>
    /// 请求不存在的键控服务时抛出
    /// </summary>
    [Fact]
    public void GetRequiredKeyedService_WhenMissing_Throws()
    {
        var counter = new CspInstanceCounter();
        using var provider = BuildProvider(counter);
        var cached = new CachedServiceProvider(provider);

        Assert.Throws<InvalidOperationException>(() => cached.GetRequiredKeyedService(typeof(CspTransientService), "missing"));
    }

    /// <summary>
    /// 缓存服务提供器经约定注册后按作用域隔离
    /// </summary>
    [Fact]
    public void CachedServiceProvider_RegisteredByConvention_IsScoped()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(new CspInstanceCounter());
        services.AddTransient<CspTransientService>();
        new DefaultConventionalRegistrar().AddType(services, typeof(CachedServiceProvider));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var otherScope = provider.CreateScope();

        var cached = scope.ServiceProvider.GetRequiredService<ICachedServiceProvider>();
        Assert.Same(cached, scope.ServiceProvider.GetRequiredService<ICachedServiceProvider>());
        Assert.NotSame(cached, otherScope.ServiceProvider.GetRequiredService<ICachedServiceProvider>());
    }

    /// <summary>
    /// 瞬时缓存服务提供器每次解析都是新的独立缓存
    /// </summary>
    [Fact]
    public void TransientCachedServiceProvider_RegisteredByConvention_IsTransient()
    {
        IServiceCollection services = new ServiceCollection();
        var counter = new CspInstanceCounter();
        services.AddSingleton(counter);
        services.AddTransient<CspTransientService>();
        new DefaultConventionalRegistrar().AddType(services, typeof(TransientCachedServiceProvider));

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITransientCachedServiceProvider>();
        var second = provider.GetRequiredService<ITransientCachedServiceProvider>();

        Assert.NotSame(first, second);
        // 两个提供器各自持有独立缓存，因此目标服务被创建两次
        first.GetService(typeof(CspTransientService));
        second.GetService(typeof(CspTransientService));
        Assert.Equal(2, counter.Created);
    }

    /// <summary>
    /// 构建带计数器与瞬时服务的服务提供器
    /// </summary>
    /// <param name="counter">实例计数器</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(CspInstanceCounter counter)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(counter);
        services.AddTransient<CspTransientService>();
        services.AddKeyedTransient<CspTransientService>("alpha");
        return services.BuildServiceProvider();
    }
}

/// <summary>
/// 实例创建计数器
/// </summary>
internal sealed class CspInstanceCounter
{
    /// <summary>
    /// 已创建次数
    /// </summary>
    public int Created { get; set; }
}

/// <summary>
/// 每次创建都会计数的瞬时服务
/// </summary>
internal sealed class CspTransientService
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="counter">实例计数器</param>
    public CspTransientService(CspInstanceCounter counter)
    {
        counter.Created++;
    }
}

/// <summary>
/// 未注册进容器的兜底类型
/// </summary>
internal sealed class CspFallback;
