// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务描述器扩展方法测试
/// </summary>
/// <remarks>
/// 键值服务与普通服务在 <see cref="ServiceDescriptor"/> 上是两套互斥的属性，
/// 读错一套要么拿到 null 要么直接抛异常。这两个扩展的全部价值就是把这道岔口收口成一次调用，
/// 因此用例必须把「键值 / 非键值」×「实例 / 类型 / 工厂」六种组合都覆盖到。
/// </remarks>
public class ServiceDescriptorExtensionsTests
{
    /// <summary>
    /// 以实例注册的非键值描述器能读回实例
    /// </summary>
    [Fact]
    public void NormalizedImplementationInstance_ForNonKeyedInstance_ReturnsInstance()
    {
        DescriptorSampleService instance = new();
        var descriptor = ServiceDescriptor.Singleton<IDescriptorSampleService>(instance);

        Assert.Same(instance, descriptor.NormalizedImplementationInstance());
    }

    /// <summary>
    /// 以实例注册的键值描述器同样能读回实例
    /// </summary>
    [Fact]
    public void NormalizedImplementationInstance_ForKeyedInstance_ReturnsInstance()
    {
        DescriptorSampleService instance = new();
        var descriptor = ServiceDescriptor.KeyedSingleton<IDescriptorSampleService>("样例", instance);

        Assert.True(descriptor.IsKeyedService);
        Assert.Same(instance, descriptor.NormalizedImplementationInstance());
    }

    /// <summary>
    /// 以类型或工厂注册的描述器没有实例可读
    /// </summary>
    [Fact]
    public void NormalizedImplementationInstance_ForTypeAndFactoryDescriptors_ReturnsNull()
    {
        Assert.Null(ServiceDescriptor.Singleton<IDescriptorSampleService, DescriptorSampleService>().NormalizedImplementationInstance());
        Assert.Null(ServiceDescriptor.KeyedSingleton<IDescriptorSampleService, DescriptorSampleService>("样例").NormalizedImplementationInstance());
        Assert.Null(ServiceDescriptor.Singleton<IDescriptorSampleService>(_ => new DescriptorSampleService()).NormalizedImplementationInstance());
        Assert.Null(ServiceDescriptor.KeyedSingleton<IDescriptorSampleService>("样例", (_, _) => new DescriptorSampleService()).NormalizedImplementationInstance());
    }

    /// <summary>
    /// 以类型注册的非键值描述器能读回实现类型
    /// </summary>
    [Fact]
    public void NormalizedImplementationType_ForNonKeyedType_ReturnsType()
    {
        var descriptor = ServiceDescriptor.Singleton<IDescriptorSampleService, DescriptorSampleService>();

        Assert.Equal(typeof(DescriptorSampleService), descriptor.NormalizedImplementationType());
    }

    /// <summary>
    /// 以类型注册的键值描述器同样能读回实现类型
    /// </summary>
    [Fact]
    public void NormalizedImplementationType_ForKeyedType_ReturnsType()
    {
        var descriptor = ServiceDescriptor.KeyedSingleton<IDescriptorSampleService, DescriptorSampleService>("样例");

        Assert.True(descriptor.IsKeyedService);
        Assert.Equal(typeof(DescriptorSampleService), descriptor.NormalizedImplementationType());
    }

    /// <summary>
    /// 以实例或工厂注册的描述器没有实现类型可读
    /// </summary>
    /// <remarks>
    /// 这正是 <c>ServiceImplementationTypeRegistry</c> 存在的原因：工厂注册的描述器丢掉了实现类型，
    /// 需要动态代理等环节另行登记，因此这条 null 语义必须固定，不能被"顺手推断"掉。
    /// </remarks>
    [Fact]
    public void NormalizedImplementationType_ForInstanceAndFactoryDescriptors_ReturnsNull()
    {
        DescriptorSampleService instance = new();

        Assert.Null(ServiceDescriptor.Singleton<IDescriptorSampleService>(instance).NormalizedImplementationType());
        Assert.Null(ServiceDescriptor.KeyedSingleton<IDescriptorSampleService>("样例", instance).NormalizedImplementationType());
        Assert.Null(ServiceDescriptor.Singleton<IDescriptorSampleService>(_ => new DescriptorSampleService()).NormalizedImplementationType());
        Assert.Null(ServiceDescriptor.KeyedSingleton<IDescriptorSampleService>("样例", (_, _) => new DescriptorSampleService()).NormalizedImplementationType());
    }

    /// <summary>
    /// 各生命周期的键值描述器都能正确取到实现类型
    /// </summary>
    /// <param name="lifetime">服务生命周期</param>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void NormalizedImplementationType_ForKeyedDescriptorOfAnyLifetime_ReturnsType(ServiceLifetime lifetime)
    {
        var descriptor = ServiceDescriptor.DescribeKeyed(
            typeof(IDescriptorSampleService),
            "样例",
            typeof(DescriptorSampleService),
            lifetime);

        Assert.Equal(typeof(DescriptorSampleService), descriptor.NormalizedImplementationType());
        Assert.Null(descriptor.NormalizedImplementationInstance());
    }
}

/// <summary>
/// 描述器测试用的服务契约
/// </summary>
public interface IDescriptorSampleService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    string Ping();
}

/// <summary>
/// 描述器测试用的服务实现
/// </summary>
public sealed class DescriptorSampleService : IDescriptorSampleService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    public string Ping()
    {
        return "descriptor";
    }
}
