// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Castle.Extensions;
using XiHan.Framework.Castle.Tests.TestDoubles;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DynamicProxy;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Castle.Tests.Extensions;

/// <summary>
/// Castle 动态代理服务集合扩展测试
/// </summary>
/// <remarks>
/// 覆盖 AddCastleDynamicProxy 的全部分支：禁用开关、空操作列表、非接口服务类型、
/// 忽略名单、无拦截器命中、以及命中后对生命周期／服务键／原有创建方式（类型、工厂、现成实例）的保持。
/// 注册回调一律按实现类型做条件过滤，避免把容器内部的对象访问器等描述器一起代理掉。
/// </remarks>
public class ServiceCollectionCastleExtensionsTests
{
    /// <summary>
    /// 扩展方法返回原服务集合实例，支持链式调用
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddCastleDynamicProxy();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 没有任何注册回调时不改动任何描述器
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_WithoutRegistrationActions_LeavesDescriptorUntouched()
    {
        var services = CreateServices();
        services.AddTransient<IGreetingService, GreetingService>();
        var before = services.Single(d => d.ServiceType == typeof(IGreetingService));

        services.AddCastleDynamicProxy();

        Assert.Same(before, services.Single(d => d.ServiceType == typeof(IGreetingService)));

        using var provider = services.BuildServiceProvider();
        Assert.False(ProxyUtil.IsProxy(provider.GetRequiredService<IGreetingService>()));
    }

    /// <summary>
    /// 显式禁用类拦截器后即使有拦截器登记也不创建代理
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_WhenClassInterceptorsDisabled_DoesNotCreateProxy()
    {
        var services = CreateServices();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);
        services.DisableClassInterceptors();

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        Assert.False(ProxyUtil.IsProxy(provider.GetRequiredService<IGreetingService>()));
    }

    /// <summary>
    /// 注册回调加了拦截器时创建代理，且拦截器真的跑到了方法上
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_WhenInterceptorRegistered_CreatesWorkingProxy()
    {
        var services = CreateServices();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        var greeting = provider.GetRequiredService<IGreetingService>();
        var text = greeting.Greet("曦寒");

        Assert.True(ProxyUtil.IsProxy(greeting));
        Assert.Same(typeof(GreetingService), ProxyUtil.GetUnproxiedInstance(greeting).GetType());
        Assert.Equal("你好，曦寒", text);

        var log = provider.GetRequiredService<CallLog>();
        Assert.Equal(1, log.Entries.Count);
        Assert.Equal("日志:Greet", log.Entries[0]);
    }

    /// <summary>
    /// 注册回调一个拦截器都没加时不创建代理
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_WhenNoInterceptorMatched_DoesNotCreateProxy()
    {
        var services = CreateServices();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(_ => { });

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        Assert.False(ProxyUtil.IsProxy(provider.GetRequiredService<IGreetingService>()));
    }

    /// <summary>
    /// 服务类型不是接口时不创建代理
    /// </summary>
    /// <remarks>
    /// 实现方式是接口代理，类类型没有可代理的契约，必须整条跳过而不是抛异常。
    /// </remarks>
    [Fact]
    public void AddCastleDynamicProxy_ForNonInterfaceServiceType_DoesNotCreateProxy()
    {
        var services = CreateServices();
        services.AddTransient<GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        Assert.False(ProxyUtil.IsProxy(provider.GetRequiredService<GreetingService>()));
    }

    /// <summary>
    /// 以工厂注册且没有登记实现类型时不创建代理
    /// </summary>
    /// <remarks>
    /// 工厂描述器自身不带实现类型，拿不到实现类型就没法跑注册回调，只能跳过。
    /// </remarks>
    [Fact]
    public void AddCastleDynamicProxy_WhenImplementationTypeUnknown_DoesNotCreateProxy()
    {
        var services = CreateServices();
        services.AddSingleton<IGreetingService>(_ => new GreetingService());
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        Assert.False(ProxyUtil.IsProxy(provider.GetRequiredService<IGreetingService>()));
    }

    /// <summary>
    /// 代理描述器保持原描述器的生命周期
    /// </summary>
    /// <param name="lifetime">原生命周期</param>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddCastleDynamicProxy_PreservesOriginalLifetime(ServiceLifetime lifetime)
    {
        var services = CreateServices();
        services.Add(ServiceDescriptor.Describe(typeof(IGreetingService), typeof(GreetingService), lifetime));
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        var descriptor = services.Single(d => d.ServiceType == typeof(IGreetingService));
        Assert.Equal(lifetime, descriptor.Lifetime);
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
    }

    /// <summary>
    /// 单例服务的代理在整个容器内只有一份
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_ForSingleton_ResolvesSameProxyInstance()
    {
        var services = CreateServices();
        services.AddSingleton<IGreetingService, GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IGreetingService>();
        var second = provider.GetRequiredService<IGreetingService>();

        Assert.True(ProxyUtil.IsProxy(first));
        Assert.Same(first, second);
    }

    /// <summary>
    /// 作用域服务的代理按作用域隔离
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_ForScoped_ResolvesProxyPerScope()
    {
        var services = CreateServices();
        services.AddScoped<IGreetingService, GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IGreetingService>();
        var firstAgain = firstScope.ServiceProvider.GetRequiredService<IGreetingService>();
        var second = secondScope.ServiceProvider.GetRequiredService<IGreetingService>();

        Assert.True(ProxyUtil.IsProxy(first));
        Assert.Same(first, firstAgain);
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 瞬时服务每次解析都是新的代理
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_ForTransient_ResolvesNewProxyEachTime()
    {
        var services = CreateServices();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IGreetingService>();
        var second = provider.GetRequiredService<IGreetingService>();

        Assert.True(ProxyUtil.IsProxy(first));
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 键值服务被代理后仍然保留原服务键
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_ForKeyedService_KeepsServiceKey()
    {
        var services = CreateServices();
        services.AddKeyedTransient<IGreetingService, GreetingService>("主");
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        var descriptor = services.Single(d => d.ServiceType == typeof(IGreetingService));
        Assert.True(descriptor.IsKeyedService);
        Assert.Equal("主", descriptor.ServiceKey as string);

        using var provider = services.BuildServiceProvider();
        var greeting = provider.GetRequiredKeyedService<IGreetingService>("主");

        Assert.True(ProxyUtil.IsProxy(greeting));
        Assert.Equal("你好，曦寒", greeting.Greet("曦寒"));
    }

    /// <summary>
    /// 以工厂注册的服务被代理后仍由原工厂创建目标实例
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_ForFactoryRegisteredService_KeepsOriginalFactory()
    {
        var services = CreateServices();
        services.AddSingleton<ITaggedService>(_ => new TaggedService("工厂"));

        var descriptor = services.Single(d => d.ServiceType == typeof(ITaggedService));
        services.GetImplementationTypeRegistry().Add(descriptor, typeof(TaggedService));
        services.OnRegistered(AddLoggingToTagged);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        var tagged = provider.GetRequiredService<ITaggedService>();

        Assert.True(ProxyUtil.IsProxy(tagged));
        Assert.Equal("工厂", tagged.Tag);
    }

    /// <summary>
    /// 以现成实例注册的服务被代理后仍复用同一个实例
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_ForInstanceRegisteredService_ReusesSameInstance()
    {
        var instance = new TaggedService("实例");
        var services = CreateServices();
        services.AddSingleton<ITaggedService>(instance);

        var descriptor = services.Single(d => d.ServiceType == typeof(ITaggedService));
        services.GetImplementationTypeRegistry().Add(descriptor, typeof(TaggedService));
        services.OnRegistered(AddLoggingToTagged);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        var tagged = provider.GetRequiredService<ITaggedService>();

        Assert.True(ProxyUtil.IsProxy(tagged));
        Assert.Same(instance, ProxyUtil.GetUnproxiedInstance(tagged));
        Assert.Equal("实例", tagged.Tag);
    }

    /// <summary>
    /// 在动态代理忽略名单里的实现类型不创建代理
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_ForIgnoredImplementationType_DoesNotCreateProxy()
    {
        DynamicProxyIgnoreTypes.Add<IgnoredService>();

        var services = CreateServices();
        services.AddTransient<IIgnoredService, IgnoredService>();
        services.OnRegistered(AddLoggingToIgnored);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        var ignored = provider.GetRequiredService<IIgnoredService>();

        Assert.False(ProxyUtil.IsProxy(ignored));
        Assert.Equal("ignored", ignored.Ping());
    }

    /// <summary>
    /// 重复调用不会把代理再套一层
    /// </summary>
    /// <remarks>
    /// 模块的 PostConfigureServices 有被多次执行的可能，套两层会让每个拦截器跑两遍。
    /// </remarks>
    [Fact]
    public void AddCastleDynamicProxy_CalledTwice_DoesNotWrapProxyTwice()
    {
        var services = CreateServices();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();
        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        var greeting = provider.GetRequiredService<IGreetingService>();
        greeting.Greet("曦寒");

        Assert.True(ProxyUtil.IsProxy(greeting));
        Assert.False(ProxyUtil.IsProxy(ProxyUtil.GetUnproxiedInstance(greeting)));
        Assert.Equal(1, provider.GetRequiredService<CallLog>().Entries.Count);
    }

    /// <summary>
    /// 多个拦截器按加入顺序执行
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_WithMultipleInterceptors_ExecutesInAddedOrder()
    {
        var services = CreateServices();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(context =>
        {
            if (context.ImplementationType == typeof(GreetingService))
            {
                context.Interceptors.TryAdd<LoggingInterceptor>();
                context.Interceptors.TryAdd<AuditInterceptor>();
            }
        });

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGreetingService>().Greet("曦寒");

        var log = provider.GetRequiredService<CallLog>();
        Assert.Equal(2, log.Entries.Count);
        Assert.Equal("日志:Greet", log.Entries[0]);
        Assert.Equal("审计:Greet", log.Entries[1]);
    }

    /// <summary>
    /// 多个注册回调依次作用于同一个上下文
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_WithMultipleRegistrationActions_AppliesAllOfThem()
    {
        var services = CreateServices();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);
        services.OnRegistered(context =>
        {
            if (context.ImplementationType == typeof(GreetingService))
            {
                context.Interceptors.TryAdd<AuditInterceptor>();
            }
        });

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGreetingService>().Greet("曦寒");

        var log = provider.GetRequiredService<CallLog>();
        Assert.Equal(2, log.Entries.Count);
        Assert.Equal("日志:Greet", log.Entries[0]);
        Assert.Equal("审计:Greet", log.Entries[1]);
    }

    /// <summary>
    /// 拦截器类型没有登记到容器时，解析被代理服务应当报错而不是静默放行
    /// </summary>
    [Fact]
    public void AddCastleDynamicProxy_WhenInterceptorTypeNotRegistered_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CallLog>();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(AddLoggingToGreeting);

        services.AddCastleDynamicProxy();

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IGreetingService>());
    }

    /// <summary>
    /// 构造带拦截器依赖的基础服务集合
    /// </summary>
    /// <returns>服务集合</returns>
    private static IServiceCollection CreateServices()
    {
        // 返回接口而非 ServiceCollection 具体类：Add(ServiceDescriptor) 是 ICollection<T> 的
        // 显式接口实现，只在接口静态类型上可见，写成具体类型会编译不过。
        IServiceCollection services = new ServiceCollection();

        services.AddSingleton<CallLog>();
        services.AddTransient<LoggingInterceptor>();
        services.AddTransient<AuditInterceptor>();

        return services;
    }

    /// <summary>
    /// 只给问候服务加日志拦截器
    /// </summary>
    /// <param name="context">服务注册上下文</param>
    private static void AddLoggingToGreeting(IOnServiceRegistredContext context)
    {
        if (context.ImplementationType == typeof(GreetingService))
        {
            context.Interceptors.TryAdd<LoggingInterceptor>();
        }
    }

    /// <summary>
    /// 只给带标记的服务加日志拦截器
    /// </summary>
    /// <param name="context">服务注册上下文</param>
    private static void AddLoggingToTagged(IOnServiceRegistredContext context)
    {
        if (context.ImplementationType == typeof(TaggedService))
        {
            context.Interceptors.TryAdd<LoggingInterceptor>();
        }
    }

    /// <summary>
    /// 只给忽略名单里的服务加日志拦截器
    /// </summary>
    /// <param name="context">服务注册上下文</param>
    private static void AddLoggingToIgnored(IOnServiceRegistredContext context)
    {
        if (context.ImplementationType == typeof(IgnoredService))
        {
            context.Interceptors.TryAdd<LoggingInterceptor>();
        }
    }
}
