// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 事件处理器工厂测试
/// </summary>
/// <remarks>
/// 三种工厂的差异全在「实例从哪来、什么时候释放、按什么口径判重」这三点上，
/// 因此逐条覆盖生命周期与 <c>IsInFactories</c> 判重口径。
/// </remarks>
public class EventHandlerFactoriesTests
{
    /// <summary>
    /// 单实例工厂拒绝空处理器
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_WhenHandlerNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new SingleInstanceHandlerFactory(null!);
        });
    }

    /// <summary>
    /// 单实例工厂每次返回同一个处理器实例
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_GetHandler_AlwaysReturnsSameInstance()
    {
        var handler = new ParameterlessLocalHandler();
        var factory = new SingleInstanceHandlerFactory(handler);

        using var first = factory.GetHandler();
        using var second = factory.GetHandler();

        Assert.Same(handler, first.EventHandler);
        Assert.Same(handler, second.EventHandler);
        Assert.Same(handler, factory.HandlerInstance);
    }

    /// <summary>
    /// 单实例工厂的包装器释放时不释放处理器本身
    /// </summary>
    /// <remarks>
    /// 单实例的所有权在调用方手里，工厂释放包装器不能顺手把共享实例销毁。
    /// </remarks>
    [Fact]
    public void SingleInstanceFactory_DisposeWrapper_KeepsHandlerAlive()
    {
        var handler = new DisposableLocalHandler();
        var factory = new SingleInstanceHandlerFactory(handler);

        factory.GetHandler().Dispose();

        Assert.False(handler.IsDisposed);
    }

    /// <summary>
    /// 单实例工厂按处理器实例判重
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_IsInFactories_MatchesBySameHandlerInstance()
    {
        var handler = new ParameterlessLocalHandler();
        var existing = new List<IEventHandlerFactory> { new SingleInstanceHandlerFactory(handler) };

        Assert.True(new SingleInstanceHandlerFactory(handler).IsInFactories(existing));
    }

    /// <summary>
    /// 同类型的不同实例不视为重复
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_IsInFactories_WithDifferentInstance_ReturnsFalse()
    {
        var existing = new List<IEventHandlerFactory> { new SingleInstanceHandlerFactory(new ParameterlessLocalHandler()) };

        Assert.False(new SingleInstanceHandlerFactory(new ParameterlessLocalHandler()).IsInFactories(existing));
    }

    /// <summary>
    /// 判重列表为空引用时抛出参数异常
    /// </summary>
    [Fact]
    public void SingleInstanceFactory_IsInFactories_WhenListNull_Throws()
    {
        var factory = new SingleInstanceHandlerFactory(new ParameterlessLocalHandler());

        Assert.Throws<ArgumentNullException>(() =>
        {
            factory.IsInFactories(null!);
        });
    }

    /// <summary>
    /// 瞬时工厂拒绝空处理器类型
    /// </summary>
    [Fact]
    public void TransientFactory_WhenHandlerTypeNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new TransientEventHandlerFactory(null!);
        });
    }

    /// <summary>
    /// 瞬时工厂每次创建新的处理器实例
    /// </summary>
    [Fact]
    public void TransientFactory_GetHandler_CreatesNewInstanceEachTime()
    {
        var factory = new TransientEventHandlerFactory(typeof(ParameterlessLocalHandler));

        using var first = factory.GetHandler();
        using var second = factory.GetHandler();

        Assert.NotSame(first.EventHandler, second.EventHandler);
        Assert.IsType<ParameterlessLocalHandler>(first.EventHandler);
    }

    /// <summary>
    /// 瞬时工厂的包装器释放时连带释放处理器
    /// </summary>
    [Fact]
    public void TransientFactory_DisposeWrapper_DisposesHandler()
    {
        var factory = new TransientEventHandlerFactory(typeof(DisposableLocalHandler));
        var wrapper = factory.GetHandler();
        var handler = (DisposableLocalHandler)wrapper.EventHandler;
        Assert.False(handler.IsDisposed);

        wrapper.Dispose();

        Assert.True(handler.IsDisposed);
    }

    /// <summary>
    /// 处理器类型没有无参构造函数时给出可定位的错误
    /// </summary>
    [Fact]
    public void TransientFactory_WhenHandlerTypeHasNoParameterlessCtor_Throws()
    {
        var factory = new TransientEventHandlerFactory(typeof(ProbeAwareLocalHandler));

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            factory.GetHandler();
        });
        Assert.Contains("无参构造函数", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(ProbeAwareLocalHandler).FullName!, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 瞬时工厂按处理器类型判重，与是否走泛型重载无关
    /// </summary>
    [Fact]
    public void TransientFactory_IsInFactories_MatchesByHandlerType()
    {
        var existing = new List<IEventHandlerFactory> { new TransientEventHandlerFactory<ParameterlessLocalHandler>() };

        Assert.True(new TransientEventHandlerFactory(typeof(ParameterlessLocalHandler)).IsInFactories(existing));
        Assert.False(new TransientEventHandlerFactory(typeof(DisposableLocalHandler)).IsInFactories(existing));
    }

    /// <summary>
    /// 泛型瞬时工厂暴露的处理器类型即泛型参数
    /// </summary>
    [Fact]
    public void TransientFactoryOfTHandler_ExposesHandlerType()
    {
        var factory = new TransientEventHandlerFactory<ParameterlessLocalHandler>();

        Assert.Equal(typeof(ParameterlessLocalHandler), factory.HandlerType);
        using var wrapper = factory.GetHandler();
        Assert.IsType<ParameterlessLocalHandler>(wrapper.EventHandler);
    }

    /// <summary>
    /// IoC 工厂拒绝空作用域工厂
    /// </summary>
    [Fact]
    public void IocFactory_WhenScopeFactoryNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new IocEventHandlerFactory(null!, typeof(ParameterlessLocalHandler));
        });
    }

    /// <summary>
    /// IoC 工厂拒绝空处理器类型
    /// </summary>
    [Fact]
    public void IocFactory_WhenHandlerTypeNull_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new IocEventHandlerFactory(provider.GetRequiredService<IServiceScopeFactory>(), null!);
        });
    }

    /// <summary>
    /// IoC 工厂从容器解析处理器
    /// </summary>
    [Fact]
    public void IocFactory_GetHandler_ResolvesFromContainer()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        services.AddTransient<ProbeAwareLocalHandler>();
        using var provider = services.BuildServiceProvider();
        var factory = new IocEventHandlerFactory(
            provider.GetRequiredService<IServiceScopeFactory>(),
            typeof(ProbeAwareLocalHandler));

        using var wrapper = factory.GetHandler();

        Assert.IsType<ProbeAwareLocalHandler>(wrapper.EventHandler);
        Assert.Equal(typeof(ProbeAwareLocalHandler), factory.HandlerType);
    }

    /// <summary>
    /// IoC 工厂每次解析都开新的作用域
    /// </summary>
    [Fact]
    public void IocFactory_GetHandler_UsesSeparateScopePerCall()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        services.AddTransient<ProbeAwareLocalHandler>();
        using var provider = services.BuildServiceProvider();
        var factory = new IocEventHandlerFactory(
            provider.GetRequiredService<IServiceScopeFactory>(),
            typeof(ProbeAwareLocalHandler));

        using var first = factory.GetHandler();
        using var second = factory.GetHandler();

        Assert.NotSame(
            ((ProbeAwareLocalHandler)first.EventHandler).Probe,
            ((ProbeAwareLocalHandler)second.EventHandler).Probe);
    }

    /// <summary>
    /// IoC 工厂的包装器释放时连带释放解析所用的作用域
    /// </summary>
    [Fact]
    public void IocFactory_DisposeWrapper_DisposesResolvedScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        services.AddTransient<ProbeAwareLocalHandler>();
        using var provider = services.BuildServiceProvider();
        var factory = new IocEventHandlerFactory(
            provider.GetRequiredService<IServiceScopeFactory>(),
            typeof(ProbeAwareLocalHandler));
        var wrapper = factory.GetHandler();
        var probe = ((ProbeAwareLocalHandler)wrapper.EventHandler).Probe;
        Assert.False(probe.IsDisposed);

        wrapper.Dispose();

        Assert.True(probe.IsDisposed);
    }

    /// <summary>
    /// 处理器未注册到容器时给出可定位的错误
    /// </summary>
    [Fact]
    public void IocFactory_WhenHandlerNotRegistered_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new IocEventHandlerFactory(
            provider.GetRequiredService<IServiceScopeFactory>(),
            typeof(ParameterlessLocalHandler));

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            factory.GetHandler();
        });
        Assert.Contains("无法从 IoC 容器解析事件处理器", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// IoC 工厂按处理器类型判重
    /// </summary>
    [Fact]
    public void IocFactory_IsInFactories_MatchesByHandlerType()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var existing = new List<IEventHandlerFactory>
        {
            new IocEventHandlerFactory(scopeFactory, typeof(ParameterlessLocalHandler))
        };

        Assert.True(new IocEventHandlerFactory(scopeFactory, typeof(ParameterlessLocalHandler)).IsInFactories(existing));
        Assert.False(new IocEventHandlerFactory(scopeFactory, typeof(DisposableLocalHandler)).IsInFactories(existing));
    }

    /// <summary>
    /// IoC 工厂自身释放不影响已解析出的处理器，也不抛异常
    /// </summary>
    [Fact]
    public void IocFactory_Dispose_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        services.AddTransient<ProbeAwareLocalHandler>();
        using var provider = services.BuildServiceProvider();
        var factory = new IocEventHandlerFactory(
            provider.GetRequiredService<IServiceScopeFactory>(),
            typeof(ProbeAwareLocalHandler));
        using var wrapper = factory.GetHandler();
        var probe = ((ProbeAwareLocalHandler)wrapper.EventHandler).Probe;

        factory.Dispose();

        Assert.False(probe.IsDisposed);
    }

    /// <summary>
    /// 不同工厂类型之间互不判重
    /// </summary>
    [Fact]
    public void IsInFactories_AcrossDifferentFactoryKinds_ReturnsFalse()
    {
        var handler = new ParameterlessLocalHandler();
        var existing = new List<IEventHandlerFactory> { new SingleInstanceHandlerFactory(handler) };

        Assert.False(new TransientEventHandlerFactory(typeof(ParameterlessLocalHandler)).IsInFactories(existing));
    }
}
