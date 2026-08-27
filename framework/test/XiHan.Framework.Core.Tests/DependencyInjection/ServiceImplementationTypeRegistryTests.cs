// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 服务实现类型登记表测试
/// </summary>
/// <remarks>
/// 以工厂委托注册的描述器自身不携带实现类型，动态代理等环节必须能按描述器实例反查。
/// 登记表以描述器实例（引用相等）为键，这里同时锁死「优先取描述器自身声明」的解析顺序。
/// </remarks>
public class ServiceImplementationTypeRegistryTests
{
    /// <summary>
    /// 以实现类型注册的描述器直接给出实现类型
    /// </summary>
    [Fact]
    public void GetDeclaredImplementationTypeOrNull_WhenTypeDescriptor_ReturnsImplementationType()
    {
        var descriptor = ServiceDescriptor.Describe(typeof(ISitrContract), typeof(SitrService), ServiceLifetime.Transient);

        Assert.Equal(typeof(SitrService), ServiceImplementationTypeRegistry.GetDeclaredImplementationTypeOrNull(descriptor));
    }

    /// <summary>
    /// 以工厂注册的描述器不携带实现类型
    /// </summary>
    [Fact]
    public void GetDeclaredImplementationTypeOrNull_WhenFactoryDescriptor_ReturnsNull()
    {
        var descriptor = ServiceDescriptor.Describe(typeof(ISitrContract), _ => new SitrService(), ServiceLifetime.Transient);

        Assert.Null(ServiceImplementationTypeRegistry.GetDeclaredImplementationTypeOrNull(descriptor));
    }

    /// <summary>
    /// 以实例注册的描述器不携带实现类型
    /// </summary>
    [Fact]
    public void GetDeclaredImplementationTypeOrNull_WhenInstanceDescriptor_ReturnsNull()
    {
        var descriptor = ServiceDescriptor.Singleton<ISitrContract>(new SitrService());

        Assert.Null(ServiceImplementationTypeRegistry.GetDeclaredImplementationTypeOrNull(descriptor));
    }

    /// <summary>
    /// 键控描述器读取键控实现类型
    /// </summary>
    [Fact]
    public void GetDeclaredImplementationTypeOrNull_WhenKeyedTypeDescriptor_ReadsKeyedImplementationType()
    {
        var descriptor = ServiceDescriptor.DescribeKeyed(typeof(ISitrContract), "k", typeof(SitrService), ServiceLifetime.Transient);

        Assert.Equal(typeof(SitrService), ServiceImplementationTypeRegistry.GetDeclaredImplementationTypeOrNull(descriptor));
    }

    /// <summary>
    /// 键控工厂描述器不携带实现类型
    /// </summary>
    [Fact]
    public void GetDeclaredImplementationTypeOrNull_WhenKeyedFactoryDescriptor_ReturnsNull()
    {
        var descriptor = ServiceDescriptor.DescribeKeyed(typeof(ISitrContract), "k", (_, _) => new SitrService(), ServiceLifetime.Transient);

        Assert.Null(ServiceImplementationTypeRegistry.GetDeclaredImplementationTypeOrNull(descriptor));
    }

    /// <summary>
    /// 未登记的描述器查不到实现类型
    /// </summary>
    [Fact]
    public void GetOrNull_WhenNotRegistered_ReturnsNull()
    {
        var registry = new ServiceImplementationTypeRegistry();
        var descriptor = ServiceDescriptor.Describe(typeof(ISitrContract), _ => new SitrService(), ServiceLifetime.Transient);

        Assert.Null(registry.GetOrNull(descriptor));
    }

    /// <summary>
    /// 登记后可按描述器实例反查实现类型
    /// </summary>
    [Fact]
    public void Add_ThenGetOrNull_ReturnsRegisteredType()
    {
        var registry = new ServiceImplementationTypeRegistry();
        var descriptor = ServiceDescriptor.Describe(typeof(ISitrContract), _ => new SitrService(), ServiceLifetime.Transient);

        registry.Add(descriptor, typeof(SitrService));

        Assert.Equal(typeof(SitrService), registry.GetOrNull(descriptor));
    }

    /// <summary>
    /// 重复登记同一描述器时后写入的生效
    /// </summary>
    [Fact]
    public void Add_WhenCalledTwice_OverwritesPreviousType()
    {
        var registry = new ServiceImplementationTypeRegistry();
        var descriptor = ServiceDescriptor.Describe(typeof(ISitrContract), _ => new SitrService(), ServiceLifetime.Transient);

        registry.Add(descriptor, typeof(SitrService));
        registry.Add(descriptor, typeof(SitrOtherService));

        Assert.Equal(typeof(SitrOtherService), registry.GetOrNull(descriptor));
    }

    /// <summary>
    /// 登记表以描述器实例为键而非按内容比较
    /// </summary>
    [Fact]
    public void GetOrNull_WhenEquivalentButDifferentInstance_ReturnsNull()
    {
        var registry = new ServiceImplementationTypeRegistry();
        var registered = ServiceDescriptor.Describe(typeof(ISitrContract), typeof(SitrService), ServiceLifetime.Transient);
        var equivalent = ServiceDescriptor.Describe(typeof(ISitrContract), typeof(SitrService), ServiceLifetime.Transient);
        registry.Add(registered, typeof(SitrService));

        Assert.Null(registry.GetOrNull(equivalent));
    }

    /// <summary>
    /// 解析实现类型时优先取描述器自身声明
    /// </summary>
    [Fact]
    public void ResolveImplementationTypeOrNull_PrefersDeclaredType()
    {
        var registry = new ServiceImplementationTypeRegistry();
        var descriptor = ServiceDescriptor.Describe(typeof(ISitrContract), typeof(SitrService), ServiceLifetime.Transient);
        registry.Add(descriptor, typeof(SitrOtherService));

        Assert.Equal(typeof(SitrService), registry.ResolveImplementationTypeOrNull(descriptor));
    }

    /// <summary>
    /// 描述器无声明时回落到登记表
    /// </summary>
    [Fact]
    public void ResolveImplementationTypeOrNull_WhenNoDeclaredType_FallsBackToRegistry()
    {
        var registry = new ServiceImplementationTypeRegistry();
        var descriptor = ServiceDescriptor.Describe(typeof(ISitrContract), _ => new SitrService(), ServiceLifetime.Transient);
        registry.Add(descriptor, typeof(SitrService));

        Assert.Equal(typeof(SitrService), registry.ResolveImplementationTypeOrNull(descriptor));
    }

    /// <summary>
    /// 两处都没有时返回空
    /// </summary>
    [Fact]
    public void ResolveImplementationTypeOrNull_WhenNothingKnown_ReturnsNull()
    {
        var registry = new ServiceImplementationTypeRegistry();
        var descriptor = ServiceDescriptor.Describe(typeof(ISitrContract), _ => new SitrService(), ServiceLifetime.Transient);

        Assert.Null(registry.ResolveImplementationTypeOrNull(descriptor));
    }
}

/// <summary>
/// 实现类型登记测试用契约
/// </summary>
internal interface ISitrContract;

/// <summary>
/// 实现类型登记测试用实现
/// </summary>
internal class SitrService : ISitrContract;

/// <summary>
/// 实现类型登记测试用另一实现
/// </summary>
internal class SitrOtherService : ISitrContract;
