// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务容器对象访问器扩展方法测试
/// </summary>
/// <remarks>
/// 对象访问器是「服务注册阶段就要占位、值等到容器建好才填」的唯一机制，框架用它回填根服务提供器。
/// 它有三条硬约定：同一类型只能登记一次（重复登记直接抛错，避免两个占位互相覆盖）；
/// 登记时插到集合最前面（后续查找走的是 FirstOrDefault，位置决定能否命中）；
/// 具体类与接口两条注册指向同一个访问器实例（否则填值和取值会落在两个对象上）。
/// </remarks>
public class ServiceCollectionObjectAccessorExtensionsTests
{
    /// <summary>
    /// 登记访问器时同时插入接口与具体类两条注册，且都排在集合最前面
    /// </summary>
    [Fact]
    public void AddObjectAccessor_InsertsInterfaceAndConcreteRegistrationsAtHead()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<AccessorPayload>();

        services.AddObjectAccessor<AccessorPayload>();

        Assert.Equal(typeof(IObjectAccessor<AccessorPayload>), services[0].ServiceType);
        Assert.Equal(typeof(ObjectAccessor<AccessorPayload>), services[1].ServiceType);
        Assert.Equal(typeof(AccessorPayload), services[2].ServiceType);
    }

    /// <summary>
    /// 两条注册指向同一个访问器实例
    /// </summary>
    [Fact]
    public void AddObjectAccessor_BothRegistrationsShareOneAccessorInstance()
    {
        IServiceCollection services = new ServiceCollection();

        var accessor = services.AddObjectAccessor<AccessorPayload>();

        using var provider = services.BuildServiceProvider();

        Assert.Same(accessor, provider.GetRequiredService<ObjectAccessor<AccessorPayload>>());
        Assert.Same(accessor, provider.GetRequiredService<IObjectAccessor<AccessorPayload>>());
    }

    /// <summary>
    /// 带初值登记时访问器立即持有该值
    /// </summary>
    [Fact]
    public void AddObjectAccessor_WithValue_KeepsValue()
    {
        IServiceCollection services = new ServiceCollection();
        AccessorPayload payload = new();

        var accessor = services.AddObjectAccessor(payload);

        Assert.Same(payload, accessor.Value);
        Assert.Same(payload, services.GetObject<AccessorPayload>());
    }

    /// <summary>
    /// 同一类型重复登记时抛出异常
    /// </summary>
    [Fact]
    public void AddObjectAccessor_CalledTwice_Throws()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddObjectAccessor<AccessorPayload>();

        var thrown = Assert.Throws<Exception>(() => services.AddObjectAccessor<AccessorPayload>());

        Assert.Contains(nameof(AccessorPayload), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 尝试登记在缺失时创建，在已存在时返回原实例
    /// </summary>
    [Fact]
    public void TryAddObjectAccessor_IsIdempotent()
    {
        IServiceCollection services = new ServiceCollection();

        var first = services.TryAddObjectAccessor<AccessorPayload>();
        var second = services.TryAddObjectAccessor<AccessorPayload>();

        Assert.Same(first, second);

        var accessorDescriptors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(ObjectAccessor<AccessorPayload>)
                || descriptor.ServiceType == typeof(IObjectAccessor<AccessorPayload>))
            .ToArray();

        Assert.Equal(2, accessorDescriptors.Length);
    }

    /// <summary>
    /// 尝试登记能接手先前用普通登记建立的访问器
    /// </summary>
    [Fact]
    public void TryAddObjectAccessor_AfterAddObjectAccessor_ReturnsExistingInstance()
    {
        IServiceCollection services = new ServiceCollection();
        var added = services.AddObjectAccessor<AccessorPayload>();

        Assert.Same(added, services.TryAddObjectAccessor<AccessorPayload>());
    }

    /// <summary>
    /// 未登记访问器时取值返回空
    /// </summary>
    [Fact]
    public void GetObjectOrNull_WhenAccessorMissing_ReturnsNull()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Null(services.GetObjectOrNull<AccessorPayload>());
    }

    /// <summary>
    /// 已登记但尚未填值时取值返回空
    /// </summary>
    /// <remarks>
    /// 「已占位但还没填值」是框架装配期的正常中间态，必须与「根本没登记」在取值上表现一致，
    /// 否则调用方无法用同一段代码同时应付这两种情况。
    /// </remarks>
    [Fact]
    public void GetObjectOrNull_WhenValueNotFilled_ReturnsNull()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddObjectAccessor<AccessorPayload>();

        Assert.Null(services.GetObjectOrNull<AccessorPayload>());
    }

    /// <summary>
    /// 登记之后填的值能被读回
    /// </summary>
    [Fact]
    public void GetObject_ReflectsValueFilledAfterRegistration()
    {
        IServiceCollection services = new ServiceCollection();
        var accessor = services.AddObjectAccessor<AccessorPayload>();
        AccessorPayload payload = new();

        accessor.Value = payload;

        Assert.Same(payload, services.GetObject<AccessorPayload>());
        Assert.Same(payload, services.GetObjectOrNull<AccessorPayload>());
    }

    /// <summary>
    /// 取不到值时抛出异常并提示应先登记访问器
    /// </summary>
    [Fact]
    public void GetObject_WhenValueMissing_ThrowsWithHint()
    {
        IServiceCollection services = new ServiceCollection();

        var thrown = Assert.Throws<Exception>(() => services.GetObject<AccessorPayload>());

        Assert.Contains(nameof(ServiceCollectionObjectAccessorExtensions.AddObjectAccessor), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 不同泛型参数的访问器互不干扰
    /// </summary>
    [Fact]
    public void ObjectAccessors_AreIsolatedPerGenericArgument()
    {
        IServiceCollection services = new ServiceCollection();
        AccessorPayload payload = new();

        services.AddObjectAccessor(payload);
        services.AddObjectAccessor<OtherAccessorPayload>();

        Assert.Same(payload, services.GetObject<AccessorPayload>());
        Assert.Null(services.GetObjectOrNull<OtherAccessorPayload>());
    }
}

/// <summary>
/// 对象访问器测试用的载荷
/// </summary>
public sealed class AccessorPayload
{
    /// <summary>
    /// 载荷标记
    /// </summary>
    public string Marker { get; } = "payload";
}

/// <summary>
/// 对象访问器测试用的另一种载荷
/// </summary>
public sealed class OtherAccessorPayload
{
    /// <summary>
    /// 载荷标记
    /// </summary>
    public string Marker { get; } = "other";
}
