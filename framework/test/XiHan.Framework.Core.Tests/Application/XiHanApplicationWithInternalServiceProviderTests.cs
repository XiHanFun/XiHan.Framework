// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Tests.Application.Fakes;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 内建服务提供器形态的曦寒应用测试
/// </summary>
/// <remarks>
/// 这一形态自己建容器，用例围绕三条契约：服务提供器只建一次、模块生命周期按 Pre/Init/Post 顺序跑、
/// 关闭钩子在显式关闭与释放兜底两条路径上各触发一次且不重复。
/// 关闭与释放的用例都先把记录器引用抓在手里再释放应用，因为释放之后容器已不可用、拿不回记录器。
/// </remarks>
public class XiHanApplicationWithInternalServiceProviderTests
{
    /// <summary>
    /// 构造后把自己登记进内建服务提供器应用契约
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Constructor_RegistersItselfAsInternalServiceProviderApplication()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(EmptyTestModule), null);

        Assert.Same(app, app.Services.GetSingletonInstanceOrNull<IXiHanApplicationWithInternalServiceProvider>());
        Assert.Same(app, app.Services.GetSingletonInstanceOrNull<IXiHanApplication>());
    }

    /// <summary>
    /// 未创建服务提供器之前作用域为空
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void ServiceScope_BeforeCreation_IsNull()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(EmptyTestModule), null);

        Assert.Null(app.ServiceScope);
    }

    /// <summary>
    /// 多次创建服务提供器返回同一个实例，不会重复建容器
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void CreateServiceProvider_CalledTwice_ReturnsSameInstance()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(EmptyTestModule), null);

        var first = app.CreateServiceProvider();
        var second = app.CreateServiceProvider();

        Assert.Same(first, second);
        Assert.Same(first, app.ServiceProvider);
        Assert.NotNull(app.ServiceScope);
        Assert.Same(first, app.ServiceScope!.ServiceProvider);
    }

    /// <summary>
    /// 先建服务提供器再初始化时沿用已建好的那个
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_AfterCreateServiceProvider_ReusesExistingProvider()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(EmptyTestModule), null);

        var created = app.CreateServiceProvider();
        app.Initialize();

        Assert.Same(created, app.ServiceProvider);
    }

    /// <summary>
    /// 重复初始化不会重建服务提供器
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_CalledTwice_KeepsSameServiceProvider()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(EmptyTestModule), null);

        app.Initialize();
        var first = app.ServiceProvider;
        app.Initialize();

        Assert.Same(first, app.ServiceProvider);
    }

    /// <summary>
    /// 设置服务提供器时把对象访问器回填成根服务提供器
    /// </summary>
    /// <remarks>
    /// 这个空壳访问器在构造期就登记好，值要等到有了容器才回填；
    /// 框架内很多延迟解析都从它取根提供器，回填失败会在运行期才暴露成空引用。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void CreateServiceProvider_BackfillsServiceProviderObjectAccessor()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(EmptyTestModule), null);

        var provider = app.CreateServiceProvider();

        Assert.Same(provider, provider.GetRequiredService<ObjectAccessor<IServiceProvider>>().Value);
        Assert.Same(provider, provider.GetRequiredService<IObjectAccessor<IServiceProvider>>().Value);
    }

    /// <summary>
    /// 同步初始化按前置、初始化、后置的顺序跑完模块生命周期
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_RunsModuleLifecycleInOrder()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(RecordingTestModule), null);

        app.Initialize();

        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(
            [
                "PreConfigureServices",
                "ConfigureServices",
                "PostConfigureServices",
                "OnPreApplicationInitialization",
                "OnApplicationInitialization",
                "OnPostApplicationInitialization"
            ],
            recorder!.Steps);
    }

    /// <summary>
    /// 异步初始化与同步初始化跑出同样的生命周期顺序
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InitializeAsync_RunsModuleLifecycleInOrder()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(RecordingTestModule), null);

        await app.InitializeAsync();

        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(
            [
                "PreConfigureServices",
                "ConfigureServices",
                "PostConfigureServices",
                "OnPreApplicationInitialization",
                "OnApplicationInitialization",
                "OnPostApplicationInitialization"
            ],
            recorder!.Steps);
    }

    /// <summary>
    /// 初始化之后模块在服务配置阶段登记的服务可以正常解析
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_ResolvesServicesRegisteredByModule()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(RecordingTestModule), null);

        app.Initialize();

        var marker = app.ServiceProvider.GetRequiredService<ModuleMarkerService>();

        Assert.Equal("marker", marker.Value);
    }

    /// <summary>
    /// 显式关闭会触发模块关闭钩子
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Shutdown_InvokesModuleShutdownHook()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(RecordingTestModule), null);
        app.Initialize();
        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>()!;

        app.Shutdown();

        Assert.Single(recorder.Steps, step => step == "OnApplicationShutdown");
    }

    /// <summary>
    /// 异步关闭同样触发模块关闭钩子
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ShutdownAsync_InvokesModuleShutdownHook()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(RecordingTestModule), null);
        await app.InitializeAsync();
        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>()!;

        await app.ShutdownAsync();

        Assert.Single(recorder.Steps, step => step == "OnApplicationShutdown");
    }

    /// <summary>
    /// 未显式关闭就释放时由释放兜底关闭一次
    /// </summary>
    /// <remarks>
    /// 控制台与单元测试这类非主机路径不会触发 ApplicationStopping，
    /// 缺了这层兜底模块的关闭钩子会被静默丢掉，因此单独锁死。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void Dispose_WhenNotShutDown_TriggersShutdownExactlyOnce()
    {
        var app = new XiHanApplicationWithInternalServiceProvider(typeof(RecordingTestModule), null);
        app.Initialize();
        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>()!;

        app.Dispose();

        Assert.Single(recorder.Steps, step => step == "OnApplicationShutdown");
    }

    /// <summary>
    /// 已显式关闭过的应用再释放不会重复触发关闭钩子
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Dispose_AfterExplicitShutdown_DoesNotShutDownTwice()
    {
        var app = new XiHanApplicationWithInternalServiceProvider(typeof(RecordingTestModule), null);
        app.Initialize();
        var recorder = app.Services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>()!;

        app.Shutdown();
        app.Dispose();

        Assert.Single(recorder.Steps, step => step == "OnApplicationShutdown");
    }

    /// <summary>
    /// 从未初始化过的应用释放时不抛异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Dispose_WithoutInitialization_DoesNotThrow()
    {
        var app = new XiHanApplicationWithInternalServiceProvider(typeof(EmptyTestModule), null);

        app.Dispose();

        Assert.Null(app.ServiceScope);
    }

    /// <summary>
    /// 模块初始化抛错时被包装成初始化异常并保留原始异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Initialize_WhenModuleThrows_WrapsIntoInitializationException()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(FailingInitializationTestModule), null);

        var thrown = Assert.Throws<InitializationException>(app.Initialize);

        var inner = Assert.IsType<InvalidOperationException>(thrown.InnerException);
        Assert.Equal("模块初始化故意失败", inner.Message);
    }

    /// <summary>
    /// 模块关闭抛错时被包装成关闭异常并保留原始异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Shutdown_WhenModuleThrows_WrapsIntoShutdownException()
    {
        using var app = new XiHanApplicationWithInternalServiceProvider(typeof(FailingShutdownTestModule), null);
        app.Initialize();

        var thrown = Assert.Throws<ShutdownException>(app.Shutdown);

        var inner = Assert.IsType<InvalidOperationException>(thrown.InnerException);
        Assert.Equal("模块关闭故意失败", inner.Message);
    }

    /// <summary>
    /// 释放兜底关闭失败时被吞掉，不干扰宿主的释放流程
    /// </summary>
    /// <remarks>
    /// 宿主可能先释放了服务提供器再释放应用，此时兜底关闭必定失败；
    /// 让释放抛错会遮蔽宿主真正的关闭原因，因此这里断言"必须安静地失败"。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void Dispose_WhenFallbackShutdownThrows_SwallowsException()
    {
        var app = new XiHanApplicationWithInternalServiceProvider(typeof(FailingShutdownTestModule), null);
        app.Initialize();

        app.Dispose();

        Assert.NotNull(app.ServiceScope);
    }
}
