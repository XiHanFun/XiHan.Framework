// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.ObjectMapping.Extensions.DependencyInjection;

namespace XiHan.Framework.ObjectMapping.Tests.Extensions.DependencyInjection;

/// <summary>
/// 对象映射服务注册扩展方法测试
/// </summary>
/// <remarks>
/// 这个扩展方法只做一件事：把 Mapster 的 IMapper 以瞬时生命周期接进容器。
/// 生命周期是重点——Mapper 内部持有可变的映射配置，被误注册成单例会造成跨请求串配置，
/// 因此除了「能解析出来」，还要断言两次解析拿到的是不同实例。
/// </remarks>
public class XiHanObjectMappingServiceCollectionExtensionsTests
{
    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanMapster_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanMapster();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 以瞬时生命周期注册 IMapper 到 Mapper 的映射
    /// </summary>
    [Fact]
    public void AddXiHanMapster_RegistersMapperAsTransient()
    {
        var services = new ServiceCollection();

        services.AddXiHanMapster();

        var descriptor = services.Single(service => service.ServiceType == typeof(IMapper));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(Mapper), descriptor.ImplementationType);
    }

    /// <summary>
    /// 注册后可以从容器解析出 Mapper 实例
    /// </summary>
    [Fact]
    public void AddXiHanMapster_ResolvesMapperFromContainer()
    {
        var services = new ServiceCollection();
        services.AddXiHanMapster();
        using var provider = services.BuildServiceProvider();

        var mapper = provider.GetRequiredService<IMapper>();

        Assert.IsType<Mapper>(mapper);
    }

    /// <summary>
    /// 瞬时生命周期意味着每次解析都拿到新实例
    /// </summary>
    [Fact]
    public void AddXiHanMapster_ResolvesNewInstanceEveryTime()
    {
        var services = new ServiceCollection();
        services.AddXiHanMapster();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IMapper>();
        var second = provider.GetRequiredService<IMapper>();

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 重复调用会叠加注册，最后一次注册的实现胜出
    /// </summary>
    /// <remarks>
    /// 内部用的是 AddTransient 而不是 TryAddTransient，所以这里锁定「叠加」而非「去重」语义，
    /// 模块被重复装配时不会抛异常，但描述符会重复出现。
    /// </remarks>
    [Fact]
    public void AddXiHanMapster_CalledTwice_AppendsAnotherDescriptor()
    {
        var services = new ServiceCollection();

        services.AddXiHanMapster();
        services.AddXiHanMapster();

        Assert.Equal(2, services.Count(service => service.ServiceType == typeof(IMapper)));
    }
}
