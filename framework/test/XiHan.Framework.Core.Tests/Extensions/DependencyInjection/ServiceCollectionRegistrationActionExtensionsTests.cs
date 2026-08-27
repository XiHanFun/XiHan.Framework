// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务集合注册操作扩展方法测试
/// </summary>
/// <remarks>
/// 这组扩展维护三份"装配期共享状态"：注册回调列表、暴露回调列表、实现类型登记表。
/// 它们都靠对象访问器存放，共同的硬约定是<b>反复获取必须拿到同一份</b>——
/// 模块 A 登记的回调要能被模块 B 之后触发的注册看到，拿到副本就等于回调静默失效。
/// 三份状态还必须互相独立，任何一份的写入都不能串到另一份上。
/// </remarks>
public class ServiceCollectionRegistrationActionExtensionsTests
{
    /// <summary>
    /// 注册回调列表多次获取拿到同一份
    /// </summary>
    [Fact]
    public void GetRegistrationActionList_ReturnsSameListAcrossCalls()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services.GetRegistrationActionList(), services.GetRegistrationActionList());
    }

    /// <summary>
    /// 登记注册回调后进入列表
    /// </summary>
    [Fact]
    public void OnRegistered_AppendsActionToList()
    {
        IServiceCollection services = new ServiceCollection();

        services.OnRegistered(_ => { });
        services.OnRegistered(_ => { });

        Assert.Equal(2, services.GetRegistrationActionList().Count);
    }

    /// <summary>
    /// 类拦截器默认启用，显式禁用后标记为真
    /// </summary>
    [Fact]
    public void DisableClassInterceptors_FlipsFlagThatDefaultsToFalse()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.False(services.IsClassInterceptorsDisabled());

        services.DisableClassInterceptors();

        Assert.True(services.IsClassInterceptorsDisabled());
        Assert.True(services.GetRegistrationActionList().IsClassInterceptorsDisabled);
    }

    /// <summary>
    /// 禁用标记落在共享列表上，之后新取到的列表同样是禁用状态
    /// </summary>
    [Fact]
    public void DisableClassInterceptors_IsVisibleThroughLaterLookups()
    {
        IServiceCollection services = new ServiceCollection();
        var before = services.GetRegistrationActionList();

        services.DisableClassInterceptors();

        Assert.Same(before, services.GetRegistrationActionList());
        Assert.True(before.IsClassInterceptorsDisabled);
    }

    /// <summary>
    /// 暴露回调列表多次获取拿到同一份
    /// </summary>
    [Fact]
    public void GetExposingActionList_ReturnsSameListAcrossCalls()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services.GetExposingActionList(), services.GetExposingActionList());
    }

    /// <summary>
    /// 登记暴露回调后进入列表
    /// </summary>
    [Fact]
    public void OnExposing_AppendsActionToList()
    {
        IServiceCollection services = new ServiceCollection();

        services.OnExposing(_ => { });

        Assert.Single(services.GetExposingActionList());
    }

    /// <summary>
    /// 实现类型登记表多次获取拿到同一份
    /// </summary>
    [Fact]
    public void GetImplementationTypeRegistry_ReturnsSameRegistryAcrossCalls()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services.GetImplementationTypeRegistry(), services.GetImplementationTypeRegistry());
    }

    /// <summary>
    /// 写进登记表的内容之后还能读回来
    /// </summary>
    [Fact]
    public void ImplementationTypeRegistry_KeepsEntriesAcrossLookups()
    {
        IServiceCollection services = new ServiceCollection();
        var descriptor = ServiceDescriptor.Singleton<IRegistrationSampleService>(_ => new RegistrationSampleService());

        services.GetImplementationTypeRegistry().Add(descriptor, typeof(RegistrationSampleService));

        Assert.Equal(typeof(RegistrationSampleService), services.GetImplementationTypeRegistry().GetOrNull(descriptor));
    }

    /// <summary>
    /// 三份共享状态互不干扰
    /// </summary>
    [Fact]
    public void SharedStates_AreIndependentOfEachOther()
    {
        IServiceCollection services = new ServiceCollection();

        services.OnRegistered(_ => { });

        Assert.Single(services.GetRegistrationActionList());
        Assert.Empty(services.GetExposingActionList());

        services.OnExposing(_ => { });

        Assert.Single(services.GetRegistrationActionList());
        Assert.Single(services.GetExposingActionList());
    }

    /// <summary>
    /// 每份共享状态只登记一条对象访问器，反复获取不会重复登记
    /// </summary>
    [Fact]
    public void SharedStates_RegisterAccessorOnlyOnce()
    {
        IServiceCollection services = new ServiceCollection();

        services.GetRegistrationActionList();
        services.GetRegistrationActionList();
        services.GetExposingActionList();
        services.GetExposingActionList();
        services.GetImplementationTypeRegistry();
        services.GetImplementationTypeRegistry();

        Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(ObjectAccessor<ServiceRegistrationActionList>)));
        Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(ObjectAccessor<ServiceExposingActionList>)));
        Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(ObjectAccessor<ServiceImplementationTypeRegistry>)));
    }

    /// <summary>
    /// 不同服务集合之间的共享状态彼此隔离
    /// </summary>
    [Fact]
    public void SharedStates_AreIsolatedPerServiceCollection()
    {
        IServiceCollection first = new ServiceCollection();
        IServiceCollection second = new ServiceCollection();

        first.OnRegistered(_ => { });
        first.DisableClassInterceptors();

        Assert.Single(first.GetRegistrationActionList());
        Assert.Empty(second.GetRegistrationActionList());
        Assert.False(second.IsClassInterceptorsDisabled());
    }
}

/// <summary>
/// 注册操作测试用的服务契约
/// </summary>
public interface IRegistrationSampleService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    string Ping();
}

/// <summary>
/// 注册操作测试用的服务实现
/// </summary>
public sealed class RegistrationSampleService : IRegistrationSampleService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    public string Ping()
    {
        return "registration";
    }
}
