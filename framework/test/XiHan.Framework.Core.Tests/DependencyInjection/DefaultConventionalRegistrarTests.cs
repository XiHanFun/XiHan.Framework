// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 默认约定注册器测试
/// </summary>
/// <remarks>
/// 约定注册是整个框架装配的入口，这里锁死四条契约：
/// 生命周期从标记接口/特性推导、暴露类型的计算、替换与尝试注册的分支、
/// 以及单例与作用域下多暴露类型经工厂重定向共享同一实例。
/// 断言一律基于真实 <see cref="ServiceCollection"/> 与真实服务提供器，不做替身。
/// </remarks>
public class DefaultConventionalRegistrarTests
{
    /// <summary>
    /// 实现 ITransientDependency 的类型注册为瞬时生命周期
    /// </summary>
    [Fact]
    public void AddType_WhenTransientMarker_RegistersAsTransient()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrTransientWorker));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrTransientWorker));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        // 瞬时生命周期不做工厂重定向，描述器直接携带实现类型
        Assert.Equal(typeof(DcrTransientWorker), descriptor.ImplementationType);
    }

    /// <summary>
    /// 实现 IScopedDependency 的类型注册为作用域生命周期
    /// </summary>
    [Fact]
    public void AddType_WhenScopedMarker_RegistersAsScoped()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrScopedWorker));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrScopedWorker));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 实现 ISingletonDependency 的类型注册为单例生命周期
    /// </summary>
    [Fact]
    public void AddType_WhenSingletonMarker_RegistersAsSingleton()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrSingletonWorker));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrSingletonWorker));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 未标记任何生命周期的类型不进入容器
    /// </summary>
    [Fact]
    public void AddType_WhenNoLifetimeMarker_RegistersNothing()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrPlainService));

        Assert.Empty(services);
    }

    /// <summary>
    /// 依赖特性上的生命周期优先于标记接口
    /// </summary>
    [Fact]
    public void AddType_WhenDependencyAttributeLifetime_OverridesMarkerInterface()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrAttributeLifetimeWorker));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrAttributeLifetimeWorker));
        // 类型同时实现 ITransientDependency，特性声明的 Singleton 必须胜出
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 标记禁止约定注册的类型不进入容器
    /// </summary>
    [Fact]
    public void AddType_WhenConventionalRegistrationDisabled_RegistersNothing()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrDisabledService));

        Assert.Empty(services);
    }

    /// <summary>
    /// 默认暴露自身与命名匹配的接口
    /// </summary>
    [Fact]
    public void AddType_WhenNoExposeAttribute_ExposesSelfAndMatchingInterface()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrTransientWorker));

        Assert.Contains(services, d => d.ServiceType == typeof(IDcrTransientWorker));
        Assert.Contains(services, d => d.ServiceType == typeof(DcrTransientWorker));
        // 生命周期标记接口本身不是业务契约，不应被暴露
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ITransientDependency));
    }

    /// <summary>
    /// 显式暴露特性只注册所列类型，不再注册自身
    /// </summary>
    [Fact]
    public void AddType_WhenExposeServicesAttribute_RegistersOnlyDeclaredTypes()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrExplicitExposedService));

        Assert.Contains(services, d => d.ServiceType == typeof(IDcrExplicitContract));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(DcrExplicitExposedService));
    }

    /// <summary>
    /// 重复添加同一类型会重复注册描述器
    /// </summary>
    /// <remarks>
    /// 默认分支走的是 Add 而非 TryAdd，重复调用并不幂等；
    /// 这是模块装配必须保证「每个类型只喂一次」的原因，故在此锁死。
    /// </remarks>
    [Fact]
    public void AddType_CalledTwice_ProducesDuplicateDescriptors()
    {
        IServiceCollection services = new ServiceCollection();
        var registrar = new DefaultConventionalRegistrar();

        registrar.AddType(services, typeof(DcrTransientWorker));
        registrar.AddType(services, typeof(DcrTransientWorker));

        var descriptors = services.Where(d => d.ServiceType == typeof(IDcrTransientWorker)).ToList();
        Assert.Equal(2, descriptors.Count);
    }

    /// <summary>
    /// 声明替换服务时覆盖已有注册
    /// </summary>
    [Fact]
    public void AddType_WhenReplaceServices_ReplacesExistingRegistration()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IDcrReplaceable, DcrOriginalReplaceable>();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrReplacementService));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrReplaceable));
        Assert.Equal(typeof(DcrReplacementService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    /// <summary>
    /// 声明尝试注册时不覆盖已有注册
    /// </summary>
    [Fact]
    public void AddType_WhenTryRegisterAndAlreadyRegistered_KeepsExistingRegistration()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IDcrTryable, DcrExistingTryable>();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrTryRegisterService));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrTryable));
        Assert.Equal(typeof(DcrExistingTryable), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 声明尝试注册且此前无注册时正常写入
    /// </summary>
    [Fact]
    public void AddType_WhenTryRegisterAndNotRegistered_AddsRegistration()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrTryRegisterService));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrTryable));
        Assert.Equal(typeof(DcrTryRegisterService), descriptor.ImplementationType);
    }

    /// <summary>
    /// 单例多暴露类型共享同一实例
    /// </summary>
    [Fact]
    public void Resolve_WhenSingletonExposesMultipleTypes_SharesSameInstance()
    {
        IServiceCollection services = new ServiceCollection();
        new DefaultConventionalRegistrar().AddType(services, typeof(DcrMultiExposedService));

        using var provider = services.BuildServiceProvider();
        var alpha = provider.GetRequiredService<IDcrAlpha>();
        var beta = provider.GetRequiredService<IDcrBeta>();
        var self = provider.GetRequiredService<DcrMultiExposedService>();

        Assert.Same(self, alpha);
        Assert.Same(self, beta);
    }

    /// <summary>
    /// 作用域多暴露类型在同一作用域内共享实例且跨作用域隔离
    /// </summary>
    [Fact]
    public void Resolve_WhenScopedExposesMultipleTypes_SharesInstanceWithinScope()
    {
        IServiceCollection services = new ServiceCollection();
        new DefaultConventionalRegistrar().AddType(services, typeof(DcrScopedWorker));

        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var contract = first.ServiceProvider.GetRequiredService<IDcrScopedWorker>();
        var self = first.ServiceProvider.GetRequiredService<DcrScopedWorker>();
        var other = second.ServiceProvider.GetRequiredService<IDcrScopedWorker>();

        Assert.Same(self, contract);
        Assert.NotSame(contract, other);
    }

    /// <summary>
    /// 单例暴露父子接口时解析父接口重定向到子接口的同一实例
    /// </summary>
    [Fact]
    public void Resolve_WhenExposedTypesHaveHierarchy_RedirectsToDerivedContract()
    {
        IServiceCollection services = new ServiceCollection();
        new DefaultConventionalRegistrar().AddType(services, typeof(DcrHierarchyService));

        using var provider = services.BuildServiceProvider();
        var derived = provider.GetRequiredService<IDcrDerivedContract>();
        var @base = provider.GetRequiredService<IDcrBaseContract>();

        Assert.Same(derived, @base);
    }

    /// <summary>
    /// 工厂式描述器的实现类型登记进实现类型登记表
    /// </summary>
    [Fact]
    public void AddType_WhenDescriptorIsFactoryBased_TracksImplementationType()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrMultiExposedService));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrAlpha));
        // 重定向后描述器以工厂注册，自身不再携带实现类型
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(typeof(DcrMultiExposedService), services.GetImplementationTypeRegistry().ResolveImplementationTypeOrNull(descriptor));
    }

    /// <summary>
    /// 暴露键值服务特性注册为键控服务
    /// </summary>
    [Fact]
    public void AddType_WhenExposeKeyedService_RegistersKeyedDescriptor()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrKeyedService));

        var descriptor = SingleDescriptorFor(services, typeof(IDcrKeyedContract));
        Assert.True(descriptor.IsKeyedService);
        Assert.Equal("primary", descriptor.ServiceKey);
        Assert.Equal(typeof(DcrKeyedService), descriptor.KeyedImplementationType);
    }

    /// <summary>
    /// 仅暴露键值服务时不再暴露默认服务
    /// </summary>
    [Fact]
    public void AddType_WhenOnlyKeyedExposed_DoesNotExposeDefaultServices()
    {
        IServiceCollection services = new ServiceCollection();
        new DefaultConventionalRegistrar().AddType(services, typeof(DcrKeyedService));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredKeyedService<IDcrKeyedContract>("primary"));
        Assert.Null(provider.GetService<IDcrKeyedContract>());
        Assert.Null(provider.GetService<DcrKeyedService>());
    }

    /// <summary>
    /// 同时声明键值与非键值暴露时两者都可解析
    /// </summary>
    [Fact]
    public void AddType_WhenKeyedAndPlainExposed_BothResolvable()
    {
        IServiceCollection services = new ServiceCollection();
        new DefaultConventionalRegistrar().AddType(services, typeof(DcrDualExposedService));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<DcrDualExposedService>(provider.GetRequiredKeyedService<IDcrDualKeyed>("dual"));
        Assert.IsType<DcrDualExposedService>(provider.GetRequiredService<IDcrDualPlain>());
    }

    /// <summary>
    /// 暴露回调可以追加暴露类型并影响最终注册
    /// </summary>
    [Fact]
    public void AddType_WhenExposingActionAddsType_RegistersAdditionalService()
    {
        IServiceCollection services = new ServiceCollection();
        Type? observedImplementationType = null;
        var observedDefaults = new List<Type>();

        services.OnExposing(context =>
        {
            observedImplementationType = context.ImplementationType;
            observedDefaults.AddRange(context.ExposedTypes.Select(t => t.ServiceType));
            context.ExposedTypes.Add(new ServiceIdentifier(typeof(IDcrHookContract)));
        });

        new DefaultConventionalRegistrar().AddType(services, typeof(DcrHookService));

        Assert.Equal(typeof(DcrHookService), observedImplementationType);
        Assert.Contains(typeof(DcrHookService), observedDefaults);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DcrHookService>(provider.GetRequiredService<IDcrHookContract>());
    }

    /// <summary>
    /// 取出指定服务类型的唯一描述器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceType">服务类型</param>
    /// <returns>唯一描述器</returns>
    private static ServiceDescriptor SingleDescriptorFor(IServiceCollection services, Type serviceType)
    {
        return Assert.Single(services.Where(d => d.ServiceType == serviceType));
    }
}

/// <summary>
/// 瞬时约定注册样例契约
/// </summary>
internal interface IDcrTransientWorker;

/// <summary>
/// 作用域约定注册样例契约
/// </summary>
internal interface IDcrScopedWorker;

/// <summary>
/// 单例约定注册样例契约
/// </summary>
internal interface IDcrSingletonWorker;

/// <summary>
/// 依赖特性覆盖生命周期样例契约
/// </summary>
internal interface IDcrAttributeLifetimeWorker;

/// <summary>
/// 显式暴露样例契约
/// </summary>
internal interface IDcrExplicitContract;

/// <summary>
/// 可替换服务样例契约
/// </summary>
internal interface IDcrReplaceable;

/// <summary>
/// 尝试注册样例契约
/// </summary>
internal interface IDcrTryable;

/// <summary>
/// 多暴露类型样例契约甲
/// </summary>
internal interface IDcrAlpha;

/// <summary>
/// 多暴露类型样例契约乙
/// </summary>
internal interface IDcrBeta;

/// <summary>
/// 父级样例契约
/// </summary>
internal interface IDcrBaseContract;

/// <summary>
/// 子级样例契约
/// </summary>
internal interface IDcrDerivedContract : IDcrBaseContract;

/// <summary>
/// 键值服务样例契约
/// </summary>
internal interface IDcrKeyedContract;

/// <summary>
/// 键值与非键值并存样例的键值契约
/// </summary>
internal interface IDcrDualKeyed;

/// <summary>
/// 键值与非键值并存样例的普通契约
/// </summary>
internal interface IDcrDualPlain;

/// <summary>
/// 暴露回调追加的样例契约
/// </summary>
internal interface IDcrHookContract;

/// <summary>
/// 瞬时约定注册样例服务
/// </summary>
internal class DcrTransientWorker : IDcrTransientWorker, ITransientDependency;

/// <summary>
/// 作用域约定注册样例服务
/// </summary>
internal class DcrScopedWorker : IDcrScopedWorker, IScopedDependency;

/// <summary>
/// 单例约定注册样例服务
/// </summary>
internal class DcrSingletonWorker : IDcrSingletonWorker, ISingletonDependency;

/// <summary>
/// 依赖特性覆盖生命周期样例服务
/// </summary>
[Dependency(ServiceLifetime.Singleton)]
internal class DcrAttributeLifetimeWorker : IDcrAttributeLifetimeWorker, ITransientDependency;

/// <summary>
/// 无生命周期标记的样例服务
/// </summary>
internal class DcrPlainService;

/// <summary>
/// 禁止约定注册的样例服务
/// </summary>
[DisableConventionalRegistration]
internal class DcrDisabledService : ITransientDependency;

/// <summary>
/// 显式暴露样例服务
/// </summary>
[ExposeServices(typeof(IDcrExplicitContract))]
internal class DcrExplicitExposedService : IDcrExplicitContract, ITransientDependency;

/// <summary>
/// 被替换掉的原始样例服务
/// </summary>
internal class DcrOriginalReplaceable : IDcrReplaceable;

/// <summary>
/// 声明替换服务的样例服务
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IDcrReplaceable))]
internal class DcrReplacementService : IDcrReplaceable, ITransientDependency;

/// <summary>
/// 已存在的尝试注册样例服务
/// </summary>
internal class DcrExistingTryable : IDcrTryable;

/// <summary>
/// 声明尝试注册的样例服务
/// </summary>
[Dependency(TryRegister = true)]
[ExposeServices(typeof(IDcrTryable))]
internal class DcrTryRegisterService : IDcrTryable, ITransientDependency;

/// <summary>
/// 同时暴露多个契约与自身的单例样例服务
/// </summary>
[ExposeServices(typeof(IDcrAlpha), typeof(IDcrBeta), typeof(DcrMultiExposedService))]
internal class DcrMultiExposedService : IDcrAlpha, IDcrBeta, ISingletonDependency;

/// <summary>
/// 暴露父子契约的单例样例服务
/// </summary>
[ExposeServices(typeof(IDcrBaseContract), typeof(IDcrDerivedContract))]
internal class DcrHierarchyService : IDcrDerivedContract, ISingletonDependency;

/// <summary>
/// 键值暴露样例服务
/// </summary>
[ExposeKeyedServiceAttribute<IDcrKeyedContract>("primary")]
internal class DcrKeyedService : IDcrKeyedContract, ISingletonDependency;

/// <summary>
/// 键值与非键值并存的样例服务
/// </summary>
[ExposeServices(typeof(IDcrDualPlain))]
[ExposeKeyedServiceAttribute<IDcrDualKeyed>("dual")]
internal class DcrDualExposedService : IDcrDualPlain, IDcrDualKeyed, ISingletonDependency;

/// <summary>
/// 供暴露回调追加契约的样例服务
/// </summary>
internal class DcrHookService : IDcrHookContract, ITransientDependency;
