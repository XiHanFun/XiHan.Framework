// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Tests.Application.Fakes;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 外部服务提供器形态的曦寒应用测试
/// </summary>
/// <remarks>
/// 这一形态由宿主（通常是通用主机）负责建容器，应用只负责接管。
/// 关键契约有两条：设置服务提供器<b>不</b>顺带初始化模块（宿主要自己挑时机初始化）；
/// 二次设置成另一个提供器必须炸掉，否则同一个应用会横跨两个容器，作用域语义彻底失效。
/// </remarks>
public class XiHanApplicationWithExternalServiceProviderTests
{
    /// <summary>
    /// 只设置服务提供器时不跑模块初始化
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SetServiceProvider_DoesNotInitializeModules()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<RecordingTestModule>(services);
        using var provider = services.BuildServiceProvider();

        app.SetServiceProvider(provider);

        Assert.Same(provider, app.ServiceProvider);

        var recorder = services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(new[] { "PreConfigureServices", "ConfigureServices", "PostConfigureServices" }, recorder!.Steps);
    }

    /// <summary>
    /// 重复设置同一个服务提供器是幂等的
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SetServiceProvider_WithSameInstanceTwice_IsIdempotent()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);
        using var provider = services.BuildServiceProvider();

        app.SetServiceProvider(provider);
        app.SetServiceProvider(provider);

        Assert.Same(provider, app.ServiceProvider);
    }

    /// <summary>
    /// 二次设置成另一个服务提供器时抛出异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SetServiceProvider_WithDifferentInstance_Throws()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);
        using var first = services.BuildServiceProvider();
        using var second = services.BuildServiceProvider();

        app.SetServiceProvider(first);

        var thrown = Assert.Throws<Exception>(() => app.SetServiceProvider(second));

        Assert.Contains("服务提供器", thrown.Message, StringComparison.Ordinal);
        Assert.Same(first, app.ServiceProvider);
    }

    /// <summary>
    /// 设置服务提供器为空时抛出参数空异常并带上参数名
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SetServiceProvider_WithNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);

        var thrown = Assert.Throws<ArgumentNullException>(() => app.SetServiceProvider(null!));

        Assert.Equal("serviceProvider", thrown.ParamName);
    }

    /// <summary>
    /// 同步初始化按前置、初始化、后置的顺序跑完模块生命周期
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_RunsModuleLifecycleInOrder()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<RecordingTestModule>(services);
        using var provider = services.BuildServiceProvider();

        app.Initialize(provider);

        var recorder = services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>()!;
        Assert.Equal(
            new[]
            {
                "PreConfigureServices",
                "ConfigureServices",
                "PostConfigureServices",
                "OnPreApplicationInitialization",
                "OnApplicationInitialization",
                "OnPostApplicationInitialization"
            },
            recorder.Steps);
    }

    /// <summary>
    /// 异步初始化与同步初始化跑出同样的生命周期顺序
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InitializeAsync_RunsModuleLifecycleInOrder()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<RecordingTestModule>(services);
        using var provider = services.BuildServiceProvider();

        await app.InitializeAsync(provider);

        var recorder = services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>()!;
        Assert.Equal(
            new[]
            {
                "PreConfigureServices",
                "ConfigureServices",
                "PostConfigureServices",
                "OnPreApplicationInitialization",
                "OnApplicationInitialization",
                "OnPostApplicationInitialization"
            },
            recorder.Steps);
    }

    /// <summary>
    /// 先设置服务提供器再用同一个实例初始化不会冲突
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_AfterSetServiceProviderWithSameInstance_Succeeds()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<RecordingTestModule>(services);
        using var provider = services.BuildServiceProvider();

        app.SetServiceProvider(provider);
        app.Initialize(provider);

        var recorder = services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>()!;
        Assert.Contains("OnApplicationInitialization", recorder.Steps);
        Assert.Same(provider, app.ServiceProvider);
    }

    /// <summary>
    /// 初始化时把对象访问器回填成宿主给的服务提供器
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_BackfillsServiceProviderObjectAccessor()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);
        using var provider = services.BuildServiceProvider();

        app.Initialize(provider);

        Assert.Same(provider, provider.GetRequiredService<IObjectAccessor<IServiceProvider>>().Value);
    }

    /// <summary>
    /// 同步初始化传入空服务提供器时抛出参数空异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_WithNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);

        var thrown = Assert.Throws<ArgumentNullException>(() => app.Initialize(null!));

        Assert.Equal("serviceProvider", thrown.ParamName);
    }

    /// <summary>
    /// 异步初始化传入空服务提供器时抛出参数空异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InitializeAsync_WithNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = new ServiceCollection();
        using var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);

        var thrown = await Assert.ThrowsAsync<ArgumentNullException>(() => app.InitializeAsync(null!));

        Assert.Equal("serviceProvider", thrown.ParamName);
    }

    /// <summary>
    /// 释放应用会连带释放宿主交来的服务提供器
    /// </summary>
    /// <remarks>
    /// 这一形态的释放语义比内建形态更重：容器不是它建的，但释放它。
    /// 宿主如果还想继续用这个容器就不能释放应用，这条契约值得单独固定。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void Dispose_DisposesExternalServiceProvider()
    {
        IServiceCollection services = new ServiceCollection();
        var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);
        var provider = services.BuildServiceProvider();
        app.Initialize(provider);

        app.Dispose();

        Assert.Throws<ObjectDisposedException>(() => provider.GetRequiredService<IXiHanApplication>());
    }

    /// <summary>
    /// 未设置服务提供器就释放时不抛异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Dispose_WithoutServiceProvider_DoesNotThrow()
    {
        IServiceCollection services = new ServiceCollection();
        var app = XiHanApplicationFactory.Create<EmptyTestModule>(services);

        app.Dispose();

        Assert.Null(app.ServiceProvider);
    }
}
