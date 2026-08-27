// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Threading.Extensions.DependencyInjection;

namespace XiHan.Framework.Threading.Tests.Extensions.DependencyInjection;

/// <summary>
/// 线程服务集合扩展测试
/// </summary>
/// <remarks>
/// 覆盖两条线：登记形态（服务类型、生命周期、实现来源、登记条数）与真实解析结果。
/// 注意该扩展本身不登记环境数据上下文，它依赖约定注册按单例依赖标记补齐，
/// 因此解析型用例先手工补上该登记再构建容器。
/// </remarks>
public class XiHanThreadingServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为空时抛出参数异常
    /// </summary>
    [Fact]
    public void AddXiHanThreading_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => XiHanThreadingServiceCollectionExtensions.AddXiHanThreading(null!));

        Assert.Equal("services", exception.ParamName);
    }

    /// <summary>
    /// 返回同一个服务集合实例，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanThreading_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanThreading();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 只登记两项服务，不做多余登记
    /// </summary>
    [Fact]
    public void AddXiHanThreading_RegistersExactlyTwoServices()
    {
        var services = new ServiceCollection();

        services.AddXiHanThreading();

        Assert.Equal(2, services.Count);
    }

    /// <summary>
    /// 令牌提供者以单例实例形式登记为空令牌提供者
    /// </summary>
    [Fact]
    public void AddXiHanThreading_RegistersNullCancellationTokenProviderAsSingletonInstance()
    {
        var services = new ServiceCollection();

        services.AddXiHanThreading();

        var descriptor = services.Single(item => item.ServiceType == typeof(ICancellationTokenProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(NullCancellationTokenProvider.Instance, descriptor.ImplementationInstance);
    }

    /// <summary>
    /// 环境作用域提供者以开放泛型单例形式登记
    /// </summary>
    [Fact]
    public void AddXiHanThreading_RegistersOpenGenericAmbientScopeProviderAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanThreading();

        var descriptor = services.Single(item => item.ServiceType == typeof(IAmbientScopeProvider<>));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(AmbientDataContextAmbientScopeProvider<>), descriptor.ImplementationType);
    }

    /// <summary>
    /// 解析出来的令牌提供者就是空令牌提供者单例
    /// </summary>
    [Fact]
    public void AddXiHanThreading_ResolvesNullCancellationTokenProviderInstance()
    {
        var services = new ServiceCollection();
        services.AddXiHanThreading();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(
            NullCancellationTokenProvider.Instance,
            serviceProvider.GetRequiredService<ICancellationTokenProvider>());
    }

    /// <summary>
    /// 补齐环境数据上下文后，任意封闭泛型的作用域提供者都能解析且保持单例
    /// </summary>
    [Fact]
    public void AddXiHanThreading_ResolvesClosedGenericScopeProviderAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAmbientDataContext, AsyncLocalAmbientDataContext>();
        services.AddXiHanThreading();

        using var serviceProvider = services.BuildServiceProvider();

        var first = serviceProvider.GetRequiredService<IAmbientScopeProvider<string>>();
        var second = serviceProvider.GetRequiredService<IAmbientScopeProvider<string>>();

        Assert.IsType<AmbientDataContextAmbientScopeProvider<string>>(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// 解析出来的作用域提供者复用容器里登记的环境数据上下文
    /// </summary>
    [Fact]
    public void AddXiHanThreading_ResolvedScopeProvider_UsesRegisteredAmbientDataContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAmbientDataContext, AsyncLocalAmbientDataContext>();
        services.AddXiHanThreading();

        using var serviceProvider = services.BuildServiceProvider();

        var scopeProvider = serviceProvider.GetRequiredService<IAmbientScopeProvider<string>>();
        var dataContext = serviceProvider.GetRequiredService<IAmbientDataContext>();
        var key = "XiHan.Framework.Threading.Tests." + Guid.NewGuid().ToString("N");

        using (scopeProvider.BeginScope(key, "值"))
        {
            Assert.NotNull(dataContext.GetData(key));
            Assert.Equal("值", scopeProvider.GetValue(key));
        }

        Assert.Null(dataContext.GetData(key));
    }
}
