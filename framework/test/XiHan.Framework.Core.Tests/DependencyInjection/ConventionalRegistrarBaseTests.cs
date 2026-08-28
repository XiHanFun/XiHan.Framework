// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 约定注册器基类模板方法测试
/// </summary>
/// <remarks>
/// 通过一个最小具体子类把受保护的模板方法开放出来，逐个验证：
/// 类型筛选、生命周期推导优先级、暴露类型计算、重定向类型选择与描述器构造。
/// 这些方法是所有派生注册器（含框架各模块自带注册器）的共同地基。
/// </remarks>
public class ConventionalRegistrarBaseTests
{
    /// <summary>
    /// 批量添加类型时逐个转交给单类型入口
    /// </summary>
    [Fact]
    public void AddTypes_ForwardsEveryTypeToAddType()
    {
        IServiceCollection services = new ServiceCollection();
        var registrar = new CrbProbeRegistrar();

        registrar.AddTypes(services, typeof(CrbNamedService), typeof(CrbPlainService));

        Assert.Equal(2, registrar.AddedTypes.Count);
        Assert.Equal(typeof(CrbNamedService), registrar.AddedTypes[0]);
        Assert.Equal(typeof(CrbPlainService), registrar.AddedTypes[1]);
    }

    /// <summary>
    /// 添加程序集时只挑选可实例化的非泛型类
    /// </summary>
    [Fact]
    public void AddAssembly_SelectsOnlyConcreteNonGenericClasses()
    {
        IServiceCollection services = new ServiceCollection();
        var registrar = new CrbProbeRegistrar();

        registrar.AddAssembly(services, typeof(DefaultConventionalRegistrar).Assembly);

        Assert.Contains(typeof(CachedServiceProvider), registrar.AddedTypes);
        Assert.DoesNotContain(typeof(ConventionalRegistrarBase), registrar.AddedTypes);
        Assert.DoesNotContain(typeof(IConventionalRegistrar), registrar.AddedTypes);
        Assert.DoesNotContain(typeof(ObjectAccessor<>), registrar.AddedTypes);
    }

    /// <summary>
    /// 依赖特性声明的生命周期优先于标记接口
    /// </summary>
    [Fact]
    public void GetLifeTimeOrNull_WhenDependencyAttributePresent_PrefersAttribute()
    {
        var registrar = new CrbProbeRegistrar();

        Assert.Equal(ServiceLifetime.Scoped, registrar.ResolveLifetime(typeof(CrbAttributeScopedService)));
    }

    /// <summary>
    /// 无依赖特性时回落到标记接口
    /// </summary>
    [Fact]
    public void GetLifeTimeOrNull_WhenOnlyMarkerInterface_UsesMarkerInterface()
    {
        var registrar = new CrbProbeRegistrar();

        Assert.Equal(ServiceLifetime.Transient, registrar.ResolveLifetime(typeof(CrbTransientService)));
    }

    /// <summary>
    /// 无特性也无标记接口时回落到默认生命周期
    /// </summary>
    [Fact]
    public void GetLifeTimeOrNull_WhenNothingDeclared_FallsBackToDefault()
    {
        var registrar = new CrbProbeRegistrar();

        Assert.Null(registrar.ResolveLifetime(typeof(CrbPlainService)));

        registrar.DefaultLifetime = ServiceLifetime.Singleton;
        Assert.Equal(ServiceLifetime.Singleton, registrar.ResolveLifetime(typeof(CrbPlainService)));
    }

    /// <summary>
    /// 同时实现多个标记接口时按瞬时、单例、作用域的顺序取第一个
    /// </summary>
    [Fact]
    public void GetServiceLifetimeFromClassHierarchy_WhenMultipleMarkers_PrefersTransient()
    {
        var registrar = new CrbProbeRegistrar();

        Assert.Equal(ServiceLifetime.Transient, registrar.ResolveHierarchyLifetime(typeof(CrbMultiMarkerService)));
    }

    /// <summary>
    /// 禁止约定注册特性可被子类继承
    /// </summary>
    [Fact]
    public void IsConventionalRegistrationDisabled_WhenAttributeOnBaseClass_AlsoDisablesDerived()
    {
        var registrar = new CrbProbeRegistrar();

        Assert.True(registrar.IsDisabled(typeof(CrbDisabledBaseService)));
        Assert.True(registrar.IsDisabled(typeof(CrbDerivedFromDisabledService)));
        Assert.False(registrar.IsDisabled(typeof(CrbPlainService)));
    }

    /// <summary>
    /// 默认暴露类型包含命名匹配的接口与自身
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenNameMatchesInterface_ExposesInterfaceAndSelf()
    {
        var registrar = new CrbProbeRegistrar();

        var exposed = registrar.ResolveExposedTypes(typeof(CrbNamedService));

        Assert.Contains(typeof(ICrbNamedService), exposed);
        Assert.Contains(typeof(CrbNamedService), exposed);
    }

    /// <summary>
    /// 接口名与类名不匹配时默认只暴露自身
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenNameDoesNotMatch_ExposesOnlySelf()
    {
        var registrar = new CrbProbeRegistrar();

        var exposed = registrar.ResolveExposedTypes(typeof(CrbUnmatchedNameService));

        Assert.Equal(typeof(CrbUnmatchedNameService), Assert.Single(exposed));
    }

    /// <summary>
    /// 无键值暴露特性时键值暴露类型为空
    /// </summary>
    [Fact]
    public void GetExposedKeyedServiceTypes_WhenNoKeyedAttribute_ReturnsEmpty()
    {
        var registrar = new CrbProbeRegistrar();

        Assert.Empty(registrar.ResolveExposedKeyedTypes(typeof(CrbNamedService)));
    }

    /// <summary>
    /// 暴露类型少于两个时不做重定向
    /// </summary>
    [Fact]
    public void GetRedirectedTypeOrNull_WhenSingleExposedType_ReturnsNull()
    {
        var registrar = new CrbProbeRegistrar();
        List<ServiceIdentifier> all = [new ServiceIdentifier(typeof(ICrbContract))];

        Assert.Null(registrar.ResolveRedirectedType(typeof(CrbContractImplementation), typeof(ICrbContract), all));
    }

    /// <summary>
    /// 暴露类型即实现类型时不做重定向
    /// </summary>
    [Fact]
    public void GetRedirectedTypeOrNull_WhenExposingSelf_ReturnsNull()
    {
        var registrar = new CrbProbeRegistrar();
        List<ServiceIdentifier> all = [new ServiceIdentifier(typeof(ICrbContract)), new ServiceIdentifier(typeof(CrbContractImplementation))];

        Assert.Null(registrar.ResolveRedirectedType(typeof(CrbContractImplementation), typeof(CrbContractImplementation), all));
    }

    /// <summary>
    /// 暴露类型中包含实现类型时重定向到实现类型
    /// </summary>
    [Fact]
    public void GetRedirectedTypeOrNull_WhenSelfExposed_RedirectsToImplementation()
    {
        var registrar = new CrbProbeRegistrar();
        List<ServiceIdentifier> all = [new ServiceIdentifier(typeof(ICrbContract)), new ServiceIdentifier(typeof(CrbContractImplementation))];

        Assert.Equal(typeof(CrbContractImplementation),
            registrar.ResolveRedirectedType(typeof(CrbContractImplementation), typeof(ICrbContract), all));
    }

    /// <summary>
    /// 未暴露自身但存在派生契约时重定向到派生契约
    /// </summary>
    [Fact]
    public void GetRedirectedTypeOrNull_WhenDerivedContractExposed_RedirectsToDerivedContract()
    {
        var registrar = new CrbProbeRegistrar();
        List<ServiceIdentifier> all = [new ServiceIdentifier(typeof(ICrbContract)), new ServiceIdentifier(typeof(ICrbDerivedContract))];

        Assert.Equal(typeof(ICrbDerivedContract),
            registrar.ResolveRedirectedType(typeof(CrbContractImplementation), typeof(ICrbContract), all));
    }

    /// <summary>
    /// 无任何可重定向目标时返回空
    /// </summary>
    [Fact]
    public void GetRedirectedTypeOrNull_WhenNoCandidate_ReturnsNull()
    {
        var registrar = new CrbProbeRegistrar();
        List<ServiceIdentifier> all = [new ServiceIdentifier(typeof(ICrbContract)), new ServiceIdentifier(typeof(ICrbUnrelatedContract))];

        Assert.Null(registrar.ResolveRedirectedType(typeof(CrbContractImplementation), typeof(ICrbContract), all));
    }

    /// <summary>
    /// 瞬时生命周期始终构造携带实现类型的描述器
    /// </summary>
    [Fact]
    public void CreateServiceDescriptor_WhenTransient_KeepsImplementationType()
    {
        var registrar = new CrbProbeRegistrar();
        List<ServiceIdentifier> all = [new ServiceIdentifier(typeof(ICrbContract)), new ServiceIdentifier(typeof(CrbContractImplementation))];

        var descriptor = registrar.BuildDescriptor(typeof(CrbContractImplementation), null, typeof(ICrbContract), all, ServiceLifetime.Transient);

        Assert.Equal(typeof(CrbContractImplementation), descriptor.ImplementationType);
        Assert.Null(descriptor.ImplementationFactory);
    }

    /// <summary>
    /// 单例生命周期发生重定向时构造工厂描述器
    /// </summary>
    [Fact]
    public void CreateServiceDescriptor_WhenSingletonRedirected_UsesFactory()
    {
        var registrar = new CrbProbeRegistrar();
        List<ServiceIdentifier> all = [new ServiceIdentifier(typeof(ICrbContract)), new ServiceIdentifier(typeof(CrbContractImplementation))];

        var descriptor = registrar.BuildDescriptor(typeof(CrbContractImplementation), null, typeof(ICrbContract), all, ServiceLifetime.Singleton);

        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 带服务键时构造键控描述器
    /// </summary>
    [Fact]
    public void CreateServiceDescriptor_WhenServiceKeyGiven_BuildsKeyedDescriptor()
    {
        var registrar = new CrbProbeRegistrar();
        List<ServiceIdentifier> all = [new ServiceIdentifier("k", typeof(ICrbContract))];

        var descriptor = registrar.BuildDescriptor(typeof(CrbContractImplementation), "k", typeof(ICrbContract), all, ServiceLifetime.Singleton);

        Assert.True(descriptor.IsKeyedService);
        Assert.Equal("k", descriptor.ServiceKey);
        Assert.Equal(typeof(CrbContractImplementation), descriptor.KeyedImplementationType);
    }

    /// <summary>
    /// 触发服务暴露时把类型列表转换为服务标识后交给回调
    /// </summary>
    [Fact]
    public void TriggerServiceExposing_WhenActionRegistered_PassesConvertedIdentifiers()
    {
        IServiceCollection services = new ServiceCollection();
        var registrar = new CrbProbeRegistrar();
        IOnServiceExposingContext? captured = null;
        services.OnExposing(context => captured = context);

        registrar.TriggerExposing(services, typeof(CrbContractImplementation), [typeof(ICrbContract)]);

        Assert.NotNull(captured);
        Assert.Equal(typeof(CrbContractImplementation), captured.ImplementationType);
        var identifier = Assert.Single(captured.ExposedTypes);
        Assert.Equal(typeof(ICrbContract), identifier.ServiceType);
        Assert.Null(identifier.ServiceKey);
    }
}

/// <summary>
/// 开放模板方法的探针注册器
/// </summary>
internal sealed class CrbProbeRegistrar : ConventionalRegistrarBase
{
    /// <summary>
    /// 已收到的类型
    /// </summary>
    public List<Type> AddedTypes { get; } = [];

    /// <summary>
    /// 默认生命周期
    /// </summary>
    public ServiceLifetime? DefaultLifetime { get; set; }

    /// <summary>
    /// 添加类型，仅记录不注册
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="type">类型</param>
    public override void AddType(IServiceCollection services, Type type)
    {
        AddedTypes.Add(type);
    }

    /// <summary>
    /// 是否禁止约定注册
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否禁止</returns>
    public bool IsDisabled(Type type)
    {
        return IsConventionalRegistrationDisabled(type);
    }

    /// <summary>
    /// 推导生命周期
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>生命周期</returns>
    public ServiceLifetime? ResolveLifetime(Type type)
    {
        return GetLifeTimeOrNull(type, GetDependencyAttributeOrNull(type));
    }

    /// <summary>
    /// 从类层次结构推导生命周期
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>生命周期</returns>
    public ServiceLifetime? ResolveHierarchyLifetime(Type type)
    {
        return GetServiceLifetimeFromClassHierarchy(type);
    }

    /// <summary>
    /// 计算暴露类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>暴露类型</returns>
    public List<Type> ResolveExposedTypes(Type type)
    {
        return GetExposedServiceTypes(type);
    }

    /// <summary>
    /// 计算键值暴露类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>键值暴露类型</returns>
    public List<ServiceIdentifier> ResolveExposedKeyedTypes(Type type)
    {
        return GetExposedKeyedServiceTypes(type);
    }

    /// <summary>
    /// 计算重定向类型
    /// </summary>
    /// <param name="implementationType">实现类型</param>
    /// <param name="exposingServiceType">当前暴露类型</param>
    /// <param name="allExposingServiceTypes">全部暴露类型</param>
    /// <returns>重定向类型</returns>
    public Type? ResolveRedirectedType(Type implementationType, Type exposingServiceType, List<ServiceIdentifier> allExposingServiceTypes)
    {
        return GetRedirectedTypeOrNull(implementationType, exposingServiceType, allExposingServiceTypes);
    }

    /// <summary>
    /// 构造服务描述器
    /// </summary>
    /// <param name="implementationType">实现类型</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="exposingServiceType">暴露类型</param>
    /// <param name="allExposingServiceTypes">全部暴露类型</param>
    /// <param name="lifetime">生命周期</param>
    /// <returns>服务描述器</returns>
    public ServiceDescriptor BuildDescriptor(Type implementationType, object? serviceKey, Type exposingServiceType,
        List<ServiceIdentifier> allExposingServiceTypes, ServiceLifetime lifetime)
    {
        return CreateServiceDescriptor(implementationType, serviceKey, exposingServiceType, allExposingServiceTypes, lifetime);
    }

    /// <summary>
    /// 触发服务暴露
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="implementationType">实现类型</param>
    /// <param name="serviceTypes">暴露类型</param>
    public void TriggerExposing(IServiceCollection services, Type implementationType, List<Type> serviceTypes)
    {
        TriggerServiceExposing(services, implementationType, serviceTypes);
    }

    /// <summary>
    /// 默认生命周期
    /// </summary>
    /// <returns>默认生命周期</returns>
    protected override ServiceLifetime? GetDefaultLifeTimeOrNull()
    {
        return DefaultLifetime;
    }
}

/// <summary>
/// 基类模板测试用契约
/// </summary>
internal interface ICrbContract;

/// <summary>
/// 基类模板测试用派生契约
/// </summary>
internal interface ICrbDerivedContract : ICrbContract;

/// <summary>
/// 基类模板测试用无关契约
/// </summary>
internal interface ICrbUnrelatedContract;

/// <summary>
/// 名称与实现匹配的契约
/// </summary>
internal interface ICrbNamedService;

/// <summary>
/// 名称与实现匹配的服务
/// </summary>
internal class CrbNamedService : ICrbNamedService;

/// <summary>
/// 名称与契约不匹配的服务
/// </summary>
internal class CrbUnmatchedNameService : ICrbContract;

/// <summary>
/// 无任何标记的服务
/// </summary>
internal class CrbPlainService;

/// <summary>
/// 仅有瞬时标记的服务
/// </summary>
internal class CrbTransientService : ITransientDependency;

/// <summary>
/// 依赖特性声明作用域生命周期的服务
/// </summary>
[Dependency(ServiceLifetime.Scoped)]
internal class CrbAttributeScopedService : ITransientDependency;

/// <summary>
/// 同时实现多个生命周期标记的服务
/// </summary>
internal class CrbMultiMarkerService : ISingletonDependency, ITransientDependency;

/// <summary>
/// 禁止约定注册的基类服务
/// </summary>
[DisableConventionalRegistration]
internal class CrbDisabledBaseService;

/// <summary>
/// 继承自禁止约定注册基类的服务
/// </summary>
internal class CrbDerivedFromDisabledService : CrbDisabledBaseService;

/// <summary>
/// 派生契约的实现
/// </summary>
internal class CrbContractImplementation : ICrbDerivedContract;
