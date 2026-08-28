// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Core.Tests.Application.Fakes;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 曦寒应用工厂测试
/// </summary>
/// <remarks>
/// 工厂的八个重载分成两族：内建服务提供器与外部服务提供器；每族又分同步与异步。
/// 异步族的关键契约是「强制把 SkipConfigureServices 置为 true，再显式跑一次 ConfigureServicesAsync」，
/// 因此不能只断言"能创建出来"，必须断言服务配置只跑了一遍、且跑在构造之后。
/// 这里创建的都是真实应用对象（会扫描 Core 与本测试程序集），故统一加超时兜底。
/// </remarks>
public class XiHanApplicationFactoryTests
{
    /// <summary>
    /// 泛型同步创建会在构造期跑完三段服务配置
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_Generic_RunsServiceConfigurationDuringConstruction()
    {
        using var app = XiHanApplicationFactory.Create<RecordingTestModule>();

        Assert.Equal(typeof(RecordingTestModule), app.StartupModuleType);

        var module = Assert.Single(app.Modules);
        Assert.Equal(typeof(RecordingTestModule), module.Type);
        Assert.IsType<RecordingTestModule>(module.Instance);
        Assert.False(module.IsLoadedAsPlugIn);

        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(["PreConfigureServices", "ConfigureServices", "PostConfigureServices"], recorder!.Steps);

        Assert.True(app.Services.IsAdded<ModuleMarkerService>());
    }

    /// <summary>
    /// 按类型同步创建与泛型重载等价
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_ByType_IsEquivalentToGenericOverload()
    {
        using var app = XiHanApplicationFactory.Create(typeof(EmptyTestModule));

        Assert.Equal(typeof(EmptyTestModule), app.StartupModuleType);
        Assert.IsAssignableFrom<IXiHanApplicationWithInternalServiceProvider>(app);
        Assert.Single(app.Modules);
    }

    /// <summary>
    /// 异步创建把服务配置推迟到构造之后，且只跑一遍
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task CreateAsync_Generic_ConfiguresServicesExactlyOnce()
    {
        using var app = await XiHanApplicationFactory.CreateAsync<RecordingTestModule>();

        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(["PreConfigureServices", "ConfigureServices", "PostConfigureServices"], recorder!.Steps);
    }

    /// <summary>
    /// 按类型异步创建同样完成服务配置
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task CreateAsync_ByType_ConfiguresServices()
    {
        using var app = await XiHanApplicationFactory.CreateAsync(typeof(RecordingTestModule));

        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(3, recorder!.Steps.Count);
    }

    /// <summary>
    /// 异步创建之后再调一次服务配置会被重复配置保护拦下
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ConfigureServicesAsync_WhenCalledTwice_ThrowsInitializationException()
    {
        using var app = await XiHanApplicationFactory.CreateAsync<EmptyTestModule>();

        var thrown = await Assert.ThrowsAsync<InitializationException>(app.ConfigureServicesAsync);

        Assert.Contains("SkipConfigureServices", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 同步创建后再显式调服务配置同样被拦下
    /// </summary>
    /// <remarks>
    /// 同步重载不会把 SkipConfigureServices 置真，构造期已经配置过一次，
    /// 这条用例把"同步创建 + 手动 ConfigureServicesAsync"这种误用固定为失败。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public async Task ConfigureServicesAsync_AfterSyncCreate_ThrowsInitializationException()
    {
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>();

        await Assert.ThrowsAsync<InitializationException>(app.ConfigureServicesAsync);
    }

    /// <summary>
    /// 显式跳过服务配置时构造期不跑任何模块配置
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task Create_WhenSkipConfigureServices_DefersModuleConfiguration()
    {
        using var app = XiHanApplicationFactory.Create<RecordingTestModule>(options => options.SkipConfigureServices = true);

        Assert.Null(app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>());

        await app.ConfigureServicesAsync();

        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(3, recorder!.Steps.Count);
    }

    /// <summary>
    /// 外部服务提供器重载直接在传入的服务集合上装配
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_WithExternalServices_RegistersItselfIntoGivenCollection()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);

        Assert.Same(services, app.Services);
        Assert.Same(app, services.GetSingletonInstanceOrNull<IXiHanApplication>());
        Assert.Same(app, services.GetSingletonInstanceOrNull<IApplicationInfoAccessor>());
        Assert.Same(app, services.GetSingletonInstanceOrNull<IModuleContainer>());
        Assert.Same(app, services.GetSingletonInstanceOrNull<IXiHanApplicationWithExternalServiceProvider>());
        Assert.NotNull(services.GetSingletonInstanceOrNull<IXiHanHostEnvironment>());
    }

    /// <summary>
    /// 按类型创建外部服务提供器应用同样可用
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_WithExternalServicesByType_BuildsApplication()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = XiHanApplicationFactory.Create(typeof(EmptyTestModule), services);

        Assert.Equal(typeof(EmptyTestModule), app.StartupModuleType);
        Assert.Same(services, app.Services);
    }

    /// <summary>
    /// 外部服务提供器的异步重载同样把服务配置推迟到构造之后
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task CreateAsync_WithExternalServices_ConfiguresServicesExactlyOnce()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = await XiHanApplicationFactory.CreateAsync<RecordingTestModule>(services);

        var recorder = services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(["PreConfigureServices", "ConfigureServices", "PostConfigureServices"], recorder!.Steps);
    }

    /// <summary>
    /// 按类型异步创建外部服务提供器应用同样完成服务配置
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task CreateAsync_WithExternalServicesByType_ConfiguresServices()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = await XiHanApplicationFactory.CreateAsync(typeof(RecordingTestModule), services);

        Assert.Same(services, app.Services);
        Assert.NotNull(services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>());
    }

    /// <summary>
    /// 选项里给出的应用名优先级最高
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void ApplicationName_FromOptions_WinsOverConfiguration()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(BuildConfigurationWithApplicationName("配置里的应用名"));

        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services, options => options.ApplicationName = "选项里的应用名");

        Assert.Equal("选项里的应用名", app.ApplicationName);
    }

    /// <summary>
    /// 未给出选项应用名时回落到配置中的同名键
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void ApplicationName_WhenOptionsEmpty_FallsBackToConfiguration()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(BuildConfigurationWithApplicationName("配置里的应用名"));

        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);

        Assert.Equal("配置里的应用名", app.ApplicationName);
    }

    /// <summary>
    /// 每个应用实例都有各自的实例标识，且是合法的全局唯一标识
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void InstanceId_IsUniquePerApplicationInstance()
    {
        using var first = XiHanApplicationFactory.Create<EmptyTestModule>();
        using var second = XiHanApplicationFactory.Create<EmptyTestModule>();

        Assert.True(Guid.TryParse(first.InstanceId, out _));
        Assert.True(Guid.TryParse(second.InstanceId, out _));
        Assert.NotEqual(first.InstanceId, second.InstanceId);
    }

    /// <summary>
    /// 选项里的环境名会写进宿主环境
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Environment_FromOptions_IsWrittenToHostEnvironment()
    {
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(options => options.Environment = "Staging");

        Assert.Equal("Staging", app.Services.GetXiHanHostEnvironment().EnvironmentName);
    }

    /// <summary>
    /// 未给出环境名时服务配置结束后兜底为生产环境
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Environment_WhenNotSpecified_FallsBackToProduction()
    {
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>();

        Assert.Equal("Production", app.Services.GetXiHanHostEnvironment().EnvironmentName);
    }

    /// <summary>
    /// 模块服务配置抛错时被包装成初始化异常，并保留原始异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_WhenModuleConfigurationThrows_WrapsIntoInitializationException()
    {
        var thrown = Assert.Throws<InitializationException>(() =>
        {
            using var app = XiHanApplicationFactory.Create<FailingConfigureServicesTestModule>();
        });

        var inner = Assert.IsType<InvalidOperationException>(thrown.InnerException);
        Assert.Equal("模块服务配置故意失败", inner.Message);
        Assert.Contains(nameof(FailingConfigureServicesTestModule), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 被依赖的模块排在启动模块之前，启动模块永远排最后
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_SortsDependenciesBeforeStartupModule()
    {
        using var app = XiHanApplicationFactory.Create<DependentTestModule>();

        Assert.Equal(2, app.Modules.Count);
        Assert.Equal(typeof(DependencyTestModule), app.Modules[0].Type);
        Assert.Equal(typeof(DependentTestModule), app.Modules[1].Type);

        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(["Dependency:ConfigureServices", "Dependent:ConfigureServices"], recorder!.Steps);
    }

    /// <summary>
    /// 默认会扫描模块所在程序集完成约定注册
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_ByDefault_RegistersModuleAssemblyByConvention()
    {
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>();

        Assert.True(app.Services.IsAdded<AutoRegisteredTestService>());
    }

    /// <summary>
    /// 模块声明跳过自动注册时不再扫描它所在的程序集
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_WhenModuleSkipsAutoServiceRegistration_DoesNotScanItsAssembly()
    {
        using var app = XiHanApplicationFactory.Create<SkipAutoRegistrationTestModule>();

        Assert.False(app.Services.IsAdded<AutoRegisteredTestService>());
    }

    /// <summary>
    /// 启动模块类型为空时抛出参数空异常并带上参数名
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_WhenStartupModuleTypeIsNull_ThrowsArgumentNullException()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() =>
        {
            using var app = XiHanApplicationFactory.Create((Type)null!);
        });

        Assert.Equal("startupModuleType", thrown.ParamName);
    }

    /// <summary>
    /// 启动模块类型不是曦寒模块时抛出参数异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Create_WhenStartupModuleTypeIsNotXiHanModule_ThrowsArgumentException()
    {
        var thrown = Assert.Throws<ArgumentException>(() =>
        {
            using var app = XiHanApplicationFactory.Create(typeof(PlainIntegrationSample));
        });

        Assert.Contains(nameof(PlainIntegrationSample), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 构造一份只带应用名键的内存配置
    /// </summary>
    /// <param name="applicationName">应用名</param>
    /// <returns>配置</returns>
    private static IConfiguration BuildConfigurationWithApplicationName(string applicationName)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationName"] = applicationName
            })
            .Build();
    }
}
