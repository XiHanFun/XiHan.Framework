// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 约定注册装配管线测试
/// </summary>
/// <remarks>
/// 管线上的四张表（注册器列表、注册回调列表、暴露回调列表、实现类型登记表）
/// 都以对象访问器为唯一存储位置，「取即建、再取同一份」是模块装配跨模块共享状态的前提，
/// 这里逐一验证其幂等性，并验证类型分发会流经全部注册器。
/// </remarks>
public class ConventionalRegistrationPipelineTests
{
    /// <summary>
    /// 首次获取注册器列表时自动放入默认注册器
    /// </summary>
    [Fact]
    public void GetConventionalRegistrars_WhenFirstCall_SeedsDefaultRegistrar()
    {
        IServiceCollection services = new ServiceCollection();

        var registrars = services.GetConventionalRegistrars();

        Assert.Contains(registrars, r => r is DefaultConventionalRegistrar);
    }

    /// <summary>
    /// 重复获取注册器列表返回同一实例
    /// </summary>
    [Fact]
    public void GetConventionalRegistrars_CalledTwice_ReturnsSameInstance()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services.GetConventionalRegistrars(), services.GetConventionalRegistrars());
    }

    /// <summary>
    /// 添加注册器后可在列表中取到并返回原服务集合
    /// </summary>
    [Fact]
    public void AddConventionalRegistrar_AppendsRegistrarAndReturnsSameCollection()
    {
        IServiceCollection services = new ServiceCollection();
        var recorder = new CrpRecordingRegistrar();

        var returned = services.AddConventionalRegistrar(recorder);

        Assert.Same(services, returned);
        Assert.Contains(recorder, services.GetConventionalRegistrars());
    }

    /// <summary>
    /// 添加单个类型时分发到全部注册器
    /// </summary>
    [Fact]
    public void AddType_DispatchesToEveryRegistrar()
    {
        IServiceCollection services = new ServiceCollection();
        var recorder = new CrpRecordingRegistrar();
        services.AddConventionalRegistrar(recorder);

        services.AddType<CrpSampleService>();

        Assert.Equal(typeof(CrpSampleService), Assert.Single(recorder.SingleTypes));
        // 默认注册器同样跑过一遍，契约与自身都进了容器
        Assert.Contains(services, d => d.ServiceType == typeof(ICrpSampleService));
    }

    /// <summary>
    /// 批量添加类型时分发到全部注册器
    /// </summary>
    [Fact]
    public void AddTypes_DispatchesBatchToEveryRegistrar()
    {
        IServiceCollection services = new ServiceCollection();
        var recorder = new CrpRecordingRegistrar();
        services.AddConventionalRegistrar(recorder);

        services.AddTypes(typeof(CrpSampleService), typeof(CrpOtherService));

        var batch = Assert.Single(recorder.BatchTypes);
        Assert.Equal(2, batch.Length);
        Assert.Equal(typeof(CrpSampleService), batch[0]);
        Assert.Equal(typeof(CrpOtherService), batch[1]);
    }

    /// <summary>
    /// 重复获取注册回调列表返回同一实例
    /// </summary>
    [Fact]
    public void GetRegistrationActionList_CalledTwice_ReturnsSameInstance()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services.GetRegistrationActionList(), services.GetRegistrationActionList());
    }

    /// <summary>
    /// 注册回调写入同一份列表
    /// </summary>
    [Fact]
    public void OnRegistered_AppendsActionToSharedList()
    {
        IServiceCollection services = new ServiceCollection();

        services.OnRegistered(_ => { });
        services.OnRegistered(_ => { });

        Assert.Equal(2, services.GetRegistrationActionList().Count);
    }

    /// <summary>
    /// 类拦截器开关默认开启且可关闭
    /// </summary>
    [Fact]
    public void DisableClassInterceptors_TogglesSharedFlag()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.False(services.IsClassInterceptorsDisabled());

        services.DisableClassInterceptors();

        Assert.True(services.IsClassInterceptorsDisabled());
        Assert.True(services.GetRegistrationActionList().IsClassInterceptorsDisabled);
    }

    /// <summary>
    /// 重复获取暴露回调列表返回同一实例
    /// </summary>
    [Fact]
    public void GetExposingActionList_CalledTwice_ReturnsSameInstance()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services.GetExposingActionList(), services.GetExposingActionList());
    }

    /// <summary>
    /// 暴露回调写入同一份列表
    /// </summary>
    [Fact]
    public void OnExposing_AppendsActionToSharedList()
    {
        IServiceCollection services = new ServiceCollection();

        services.OnExposing(_ => { });

        Assert.Single(services.GetExposingActionList());
    }

    /// <summary>
    /// 重复获取实现类型登记表返回同一实例
    /// </summary>
    [Fact]
    public void GetImplementationTypeRegistry_CalledTwice_ReturnsSameInstance()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services.GetImplementationTypeRegistry(), services.GetImplementationTypeRegistry());
    }

    /// <summary>
    /// 管线上的共享表都以对象访问器形式落在服务集合里
    /// </summary>
    [Fact]
    public void SharedLists_AreStoredThroughObjectAccessor()
    {
        IServiceCollection services = new ServiceCollection();

        var actionList = services.GetRegistrationActionList();

        Assert.Same(actionList, services.GetObject<ServiceRegistrationActionList>());
    }

    /// <summary>
    /// 未放入对象时按契约返回空或抛出
    /// </summary>
    [Fact]
    public void GetObject_WhenAccessorMissing_ReturnsNullOrThrows()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Null(services.GetObjectOrNull<ServiceExposingActionList>());
        Assert.Throws<Exception>(() => services.GetObject<ServiceExposingActionList>());
    }
}

/// <summary>
/// 记录分发结果的注册器
/// </summary>
internal sealed class CrpRecordingRegistrar : IConventionalRegistrar
{
    /// <summary>
    /// 收到的单个类型
    /// </summary>
    public List<Type> SingleTypes { get; } = [];

    /// <summary>
    /// 收到的批量类型
    /// </summary>
    public List<Type[]> BatchTypes { get; } = [];

    /// <summary>
    /// 收到的程序集
    /// </summary>
    public List<Assembly> Assemblies { get; } = [];

    /// <summary>
    /// 添加程序集
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assembly">程序集</param>
    public void AddAssembly(IServiceCollection services, Assembly assembly)
    {
        Assemblies.Add(assembly);
    }

    /// <summary>
    /// 添加多个类型
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="types">类型集合</param>
    public void AddTypes(IServiceCollection services, params Type[] types)
    {
        BatchTypes.Add(types);
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="type">类型</param>
    public void AddType(IServiceCollection services, Type type)
    {
        SingleTypes.Add(type);
    }
}

/// <summary>
/// 管线测试样例契约
/// </summary>
internal interface ICrpSampleService;

/// <summary>
/// 管线测试样例服务
/// </summary>
internal class CrpSampleService : ICrpSampleService, ITransientDependency;

/// <summary>
/// 管线测试另一样例服务
/// </summary>
internal class CrpOtherService : ITransientDependency;
