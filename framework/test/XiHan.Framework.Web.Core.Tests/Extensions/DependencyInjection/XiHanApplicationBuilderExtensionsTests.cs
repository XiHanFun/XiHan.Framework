// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Tests.Infrastructure;

namespace XiHan.Framework.Web.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 曦寒应用程序构建器扩展测试
/// </summary>
/// <remarks>
/// InitializeApplication 是 Web 宿主接入模块系统的唯一入口，它必须做完整三件事：
/// 把 IApplicationBuilder 回填进对象访问器（否则模块拿不到管线）、把关闭与释放挂到宿主生命周期、
/// 再把根服务提供器交给应用初始化。用手写替身观察这三件事的调用次数与顺序，不启动真实模块系统。
/// </remarks>
public class XiHanApplicationBuilderExtensionsTests
{
    /// <summary>
    /// 同步初始化会回填对象访问器并把根服务提供器交给应用
    /// </summary>
    [Fact]
    public void InitializeApplication_FillsAccessorAndInitializesApplication()
    {
        using var host = CreateHost();

        host.App.InitializeApplication();

        Assert.Same(host.App, host.Accessor.Value);
        Assert.Equal(1, host.Application.InitializeCallCount);
        Assert.Equal(0, host.Application.InitializeAsyncCallCount);
        Assert.Same(host.Provider, host.Application.InitializedServiceProvider);
    }

    /// <summary>
    /// 同步初始化后，宿主停止时走同步关闭，停止完成时释放应用
    /// </summary>
    [Fact]
    public void InitializeApplication_RegistersSynchronousShutdownAndDispose()
    {
        using var host = CreateHost();

        host.App.InitializeApplication();

        Assert.Equal(0, host.Application.ShutdownCallCount);
        Assert.Equal(0, host.Application.DisposeCallCount);

        host.Lifetime.RaiseStopping();

        Assert.Equal(1, host.Application.ShutdownCallCount);
        Assert.Equal(0, host.Application.ShutdownAsyncCallCount);
        Assert.Equal(0, host.Application.DisposeCallCount);

        host.Lifetime.RaiseStopped();

        Assert.Equal(1, host.Application.DisposeCallCount);
    }

    /// <summary>
    /// 异步初始化走异步入口，且同样回填对象访问器
    /// </summary>
    [Fact]
    public async Task InitializeApplicationAsync_FillsAccessorAndInitializesApplication()
    {
        using var host = CreateHost();

        await host.App.InitializeApplicationAsync();

        Assert.Same(host.App, host.Accessor.Value);
        Assert.Equal(1, host.Application.InitializeAsyncCallCount);
        Assert.Equal(0, host.Application.InitializeCallCount);
        Assert.Same(host.Provider, host.Application.InitializedServiceProvider);
    }

    /// <summary>
    /// 异步初始化后，宿主停止时走异步关闭，停止完成时释放应用
    /// </summary>
    [Fact]
    public async Task InitializeApplicationAsync_RegistersAsynchronousShutdownAndDispose()
    {
        using var host = CreateHost();

        await host.App.InitializeApplicationAsync();

        host.Lifetime.RaiseStopping();

        Assert.Equal(1, host.Application.ShutdownAsyncCallCount);
        Assert.Equal(0, host.Application.ShutdownCallCount);

        host.Lifetime.RaiseStopped();

        Assert.Equal(1, host.Application.DisposeCallCount);
    }

    /// <summary>
    /// 传入空管线构建器时抛参数空异常
    /// </summary>
    [Fact]
    public void InitializeApplication_WhenAppIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => XiHanApplicationBuilderExtensions.InitializeApplication(null!));

        Assert.Equal("app", exception.ParamName);
    }

    /// <summary>
    /// 异步版本传入空管线构建器时同样抛参数空异常
    /// </summary>
    [Fact]
    public async Task InitializeApplicationAsync_WhenAppIsNull_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => XiHanApplicationBuilderExtensions.InitializeApplicationAsync(null!));

        Assert.Equal("app", exception.ParamName);
    }

    /// <summary>
    /// 容器里缺少对象访问器时属于装配缺陷，初始化直接失败而不是静默跳过
    /// </summary>
    [Fact]
    public void InitializeApplication_WhenObjectAccessorMissing_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IXiHanApplicationWithExternalServiceProvider>(new FakeXiHanApplication());
        using var lifetime = new FakeHostApplicationLifetime();
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        Assert.Throws<InvalidOperationException>(() => app.InitializeApplication());
    }

    /// <summary>
    /// 容器里缺少曦寒应用时初始化失败
    /// </summary>
    [Fact]
    public void InitializeApplication_WhenApplicationMissing_Throws()
    {
        var services = new ServiceCollection();
        services.AddObjectAccessor<IApplicationBuilder>();
        using var lifetime = new FakeHostApplicationLifetime();
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        Assert.Throws<InvalidOperationException>(() => app.InitializeApplication());
    }

    /// <summary>
    /// 组装一套可初始化的最小宿主
    /// </summary>
    /// <returns>宿主组件集合</returns>
    private static ApplicationHost CreateHost()
    {
        var services = new ServiceCollection();
        var accessor = services.AddObjectAccessor<IApplicationBuilder>();
        var application = new FakeXiHanApplication();
        var lifetime = new FakeHostApplicationLifetime();

        services.AddSingleton<IXiHanApplicationWithExternalServiceProvider>(application);
        services.AddSingleton<IHostApplicationLifetime>(lifetime);

        var provider = services.BuildServiceProvider();

        return new ApplicationHost
        {
            Provider = provider,
            Accessor = accessor,
            Application = application,
            Lifetime = lifetime,
            App = new ApplicationBuilder(provider)
        };
    }

    /// <summary>
    /// 一次用例所需的宿主组件
    /// </summary>
    private sealed class ApplicationHost : IDisposable
    {
        /// <summary>
        /// 根服务提供器
        /// </summary>
        public required ServiceProvider Provider { get; init; }

        /// <summary>
        /// 管线构建器的对象访问器
        /// </summary>
        public required ObjectAccessor<IApplicationBuilder> Accessor { get; init; }

        /// <summary>
        /// 曦寒应用替身
        /// </summary>
        public required FakeXiHanApplication Application { get; init; }

        /// <summary>
        /// 宿主生命周期替身
        /// </summary>
        public required FakeHostApplicationLifetime Lifetime { get; init; }

        /// <summary>
        /// 管线构建器
        /// </summary>
        public required ApplicationBuilder App { get; init; }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            Lifetime.Dispose();
            Provider.Dispose();
        }
    }
}
