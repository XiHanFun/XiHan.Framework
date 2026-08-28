// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Reflections;
using XiHan.Framework.Core.Tests.Application.Fakes;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务容器常用扩展方法测试
/// </summary>
/// <remarks>
/// 这组扩展是框架在「容器还没建好」阶段读取已注册单例实例的唯一手段，
/// 因此它只看服务描述器上的实例，不会触发任何解析。三条边界最关键：
/// 只以实例注册的才读得到（类型注册与工厂注册一律读不到）；键值与非键值统一走规范化读取；
/// 找不到时必须抛错而不是返回默认值，否则装配期的缺失会拖到运行期才炸。
/// </remarks>
public class ServiceCollectionCommonExtensionsTests
{
    /// <summary>
    /// 已添加判定按服务类型匹配
    /// </summary>
    [Fact]
    public void IsAdded_MatchesByServiceType()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<ICommonSampleService, CommonSampleService>();

        Assert.True(services.IsAdded<ICommonSampleService>());
        Assert.True(services.IsAdded(typeof(ICommonSampleService)));
        Assert.False(services.IsAdded<CommonSampleService>());
        Assert.False(services.IsAdded(typeof(IDisposable)));
    }

    /// <summary>
    /// 空集合上的已添加判定为假
    /// </summary>
    [Fact]
    public void IsAdded_OnEmptyCollection_IsFalse()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.False(services.IsAdded<ICommonSampleService>());
    }

    /// <summary>
    /// 以实例注册时能读回该实例
    /// </summary>
    [Fact]
    public void GetSingletonInstanceOrNull_ForInstanceRegistration_ReturnsInstance()
    {
        IServiceCollection services = new ServiceCollection();
        CommonSampleService instance = new();
        services.AddSingleton<ICommonSampleService>(instance);

        Assert.Same(instance, services.GetSingletonInstanceOrNull<ICommonSampleService>());
        Assert.Same(instance, services.GetSingletonInstance<ICommonSampleService>());
    }

    /// <summary>
    /// 以键值实例注册时同样能读回实例
    /// </summary>
    /// <remarks>
    /// 键值服务的实例存在另一个属性上，规范化读取正是为此存在；
    /// 少了这层规范化，键值注册的单例在装配期一律读成 null。
    /// </remarks>
    [Fact]
    public void GetSingletonInstanceOrNull_ForKeyedInstanceRegistration_ReturnsInstance()
    {
        IServiceCollection services = new ServiceCollection();
        CommonSampleService instance = new();
        services.AddKeyedSingleton<ICommonSampleService>("样例", instance);

        Assert.Same(instance, services.GetSingletonInstanceOrNull<ICommonSampleService>());
    }

    /// <summary>
    /// 以类型或工厂注册时读不到实例
    /// </summary>
    [Fact]
    public void GetSingletonInstanceOrNull_ForTypeAndFactoryRegistration_ReturnsNull()
    {
        IServiceCollection typeRegistered = new ServiceCollection();
        typeRegistered.AddSingleton<ICommonSampleService, CommonSampleService>();

        IServiceCollection factoryRegistered = new ServiceCollection();
        factoryRegistered.AddSingleton<ICommonSampleService>(_ => new CommonSampleService());

        Assert.Null(typeRegistered.GetSingletonInstanceOrNull<ICommonSampleService>());
        Assert.Null(factoryRegistered.GetSingletonInstanceOrNull<ICommonSampleService>());
    }

    /// <summary>
    /// 未注册时读取返回空
    /// </summary>
    [Fact]
    public void GetSingletonInstanceOrNull_WhenMissing_ReturnsNull()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Null(services.GetSingletonInstanceOrNull<ICommonSampleService>());
    }

    /// <summary>
    /// 未注册时强制读取抛出无效操作异常并带上类型名
    /// </summary>
    [Fact]
    public void GetSingletonInstance_WhenMissing_ThrowsWithTypeName()
    {
        IServiceCollection services = new ServiceCollection();

        var thrown = Assert.Throws<InvalidOperationException>(() => services.GetSingletonInstance<ICommonSampleService>());

        Assert.Contains(nameof(ICommonSampleService), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 类型查找器按单例实例读取
    /// </summary>
    [Fact]
    public void GetTypeFinder_ReadsRegisteredInstance()
    {
        IServiceCollection services = new ServiceCollection();
        FakeTypeFinder finder = new();
        services.AddSingleton<ITypeFinder>(finder);

        Assert.Same(finder, services.GetTypeFinder());
    }

    /// <summary>
    /// 未注册类型查找器时抛出无效操作异常
    /// </summary>
    [Fact]
    public void GetTypeFinder_WhenMissing_Throws()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.GetTypeFinder());
    }

    /// <summary>
    /// 没有服务提供器工厂时回落到默认容器构建
    /// </summary>
    [Fact]
    public void BuildServiceProviderFromFactory_WithoutFactory_FallsBackToDefaultProvider()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<ICommonSampleService, CommonSampleService>();

        var provider = services.BuildServiceProviderFromFactory();

        try
        {
            Assert.NotNull(provider.GetService<ICommonSampleService>());
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 注册了服务提供器工厂时改由工厂构建容器
    /// </summary>
    [Fact]
    public void BuildServiceProviderFromFactory_WithRegisteredFactory_DelegatesToFactory()
    {
        IServiceCollection services = new ServiceCollection();
        RecordingServiceProviderFactory factory = new();
        services.AddSingleton<IServiceProviderFactory<RecordingContainerBuilder>>(factory);
        services.AddSingleton<ICommonSampleService, CommonSampleService>();

        var provider = services.BuildServiceProviderFromFactory();

        try
        {
            Assert.Equal(1, factory.CreateBuilderCallCount);
            Assert.Equal(1, factory.CreateServiceProviderCallCount);
            Assert.NotNull(provider.GetService<ICommonSampleService>());
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 泛型重载会把构建器交给调用方定制
    /// </summary>
    [Fact]
    public void BuildServiceProviderFromFactory_Generic_InvokesBuilderAction()
    {
        IServiceCollection services = new ServiceCollection();
        RecordingServiceProviderFactory factory = new();
        services.AddSingleton<IServiceProviderFactory<RecordingContainerBuilder>>(factory);

        RecordingContainerBuilder? customized = null;
        var provider = services.BuildServiceProviderFromFactory<RecordingContainerBuilder>(builder =>
        {
            builder.Customized = true;
            customized = builder;
        });

        try
        {
            Assert.NotNull(customized);
            Assert.True(customized!.Customized);
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 泛型重载在找不到对应工厂时抛出框架异常
    /// </summary>
    [Fact]
    public void BuildServiceProviderFromFactory_Generic_WhenFactoryMissing_ThrowsXiHanException()
    {
        IServiceCollection services = new ServiceCollection();

        var thrown = Assert.Throws<XiHanException>(() => services.BuildServiceProviderFromFactory<RecordingContainerBuilder>());

        Assert.Contains(nameof(RecordingContainerBuilder), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 服务集合为空时构建容器抛出参数空异常
    /// </summary>
    [Fact]
    public void BuildServiceProviderFromFactory_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Equal("services", Assert.Throws<ArgumentNullException>(() => services.BuildServiceProviderFromFactory()).ParamName);
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(() => services.BuildServiceProviderFromFactory<RecordingContainerBuilder>()).ParamName);
    }

    /// <summary>
    /// 延迟解析在取值之前不触碰容器
    /// </summary>
    /// <remarks>
    /// 这组扩展的使用场景是「注册阶段就写下解析意图、初始化之后才真正解析」，
    /// 因此"没访问 Value 就不解析"是它的全部价值，必须单独锁死。
    /// </remarks>
    [Fact]
    public void ServiceLazies_AreNotEvaluatedBeforeValueIsRead()
    {
        IServiceCollection services = new ServiceCollection();

        var byType = services.GetServiceLazy<ICommonSampleService>();
        var byRuntimeType = services.GetServiceLazy(typeof(ICommonSampleService));
        var requiredByType = services.GetRequiredServiceLazy<ICommonSampleService>();
        var requiredByRuntimeType = services.GetRequiredServiceLazy(typeof(ICommonSampleService));

        Assert.False(byType.IsValueCreated);
        Assert.False(byRuntimeType.IsValueCreated);
        Assert.False(requiredByType.IsValueCreated);
        Assert.False(requiredByRuntimeType.IsValueCreated);
    }

    /// <summary>
    /// 应用初始化之后可以经服务集合解析出模块登记的服务
    /// </summary>
    /// <remarks>
    /// 这几个扩展的实现是「先从集合里取出曦寒应用，再走应用的服务提供器」，
    /// 所以它们必须在应用初始化之后才可用，这条前置条件用真实应用来验证。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void GetRequiredService_AfterApplicationInitialization_ResolvesFromApplicationProvider()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<RecordingTestModule>(services);
        using var provider = services.BuildServiceProvider();
        app.Initialize(provider);

        var byType = services.GetRequiredService<ModuleMarkerService>();
        var byRuntimeType = services.GetRequiredService(typeof(ModuleMarkerService));

        Assert.Equal("marker", byType.Value);
        Assert.Same(byType, byRuntimeType);
    }

    /// <summary>
    /// 应用初始化之后延迟解析取值成功且只解析一次
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void ServiceLazies_AfterApplicationInitialization_ResolveOnFirstRead()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<RecordingTestModule>(services);
        using var provider = services.BuildServiceProvider();
        app.Initialize(provider);

        var lazy = services.GetRequiredServiceLazy<ModuleMarkerService>();
        var lazyByRuntimeType = services.GetServiceLazy(typeof(ModuleMarkerService));

        Assert.False(lazy.IsValueCreated);

        var first = lazy.Value;
        var second = lazy.Value;

        Assert.Same(first, second);
        Assert.True(lazy.IsValueCreated);
        Assert.NotNull(lazyByRuntimeType.Value);
    }

    /// <summary>
    /// 未登记曦寒应用时经服务集合解析直接失败
    /// </summary>
    [Fact]
    public void GetRequiredService_WithoutApplication_Throws()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<ICommonSampleService>());
        Assert.Throws<InvalidOperationException>(() => services.GetRequiredService(typeof(ICommonSampleService)));
    }

    /// <summary>
    /// 未填对象访问器时取不到服务提供器
    /// </summary>
    [Fact]
    public void GetServiceProviderOrNull_WhenAccessorNotFilled_ReturnsNull()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Null(services.GetServiceProviderOrNull());
    }

    /// <summary>
    /// 应用初始化之后能经服务集合取到根服务提供器
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetServiceProviderOrNull_AfterApplicationInitialization_ReturnsRootProvider()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);
        using var provider = services.BuildServiceProvider();

        Assert.Null(services.GetServiceProviderOrNull());

        app.Initialize(provider);

        Assert.Same(provider, services.GetServiceProviderOrNull());
    }
}

/// <summary>
/// 常用扩展测试用的服务契约
/// </summary>
public interface ICommonSampleService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    string Ping();
}

/// <summary>
/// 常用扩展测试用的服务实现
/// </summary>
public sealed class CommonSampleService : ICommonSampleService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    public string Ping()
    {
        return "common";
    }
}

/// <summary>
/// 手写的类型查找器替身
/// </summary>
public sealed class FakeTypeFinder : ITypeFinder
{
    /// <summary>
    /// 类型列表
    /// </summary>
    public IReadOnlyList<Type> Types { get; } = [typeof(FakeTypeFinder)];
}

/// <summary>
/// 记录调用次数的容器构建器
/// </summary>
public sealed class RecordingContainerBuilder
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="services">服务集合</param>
    public RecordingContainerBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// 服务集合
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 是否被调用方定制过
    /// </summary>
    public bool Customized { get; set; }
}

/// <summary>
/// 记录调用次数的服务提供器工厂替身
/// </summary>
public sealed class RecordingServiceProviderFactory : IServiceProviderFactory<RecordingContainerBuilder>
{
    /// <summary>
    /// 创建构建器的调用次数
    /// </summary>
    public int CreateBuilderCallCount { get; private set; }

    /// <summary>
    /// 创建服务提供器的调用次数
    /// </summary>
    public int CreateServiceProviderCallCount { get; private set; }

    /// <summary>
    /// 创建容器构建器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>容器构建器</returns>
    public RecordingContainerBuilder CreateBuilder(IServiceCollection services)
    {
        CreateBuilderCallCount++;
        return new RecordingContainerBuilder(services);
    }

    /// <summary>
    /// 创建服务提供器
    /// </summary>
    /// <param name="containerBuilder">容器构建器</param>
    /// <returns>服务提供器</returns>
    public IServiceProvider CreateServiceProvider(RecordingContainerBuilder containerBuilder)
    {
        CreateServiceProviderCallCount++;
        return containerBuilder.Services.BuildServiceProvider();
    }
}
