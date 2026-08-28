// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.Hosting;
using XiHan.Framework.Core.Tests.Application.Fakes;

namespace XiHan.Framework.Core.Tests.Extensions.Hosting;

/// <summary>
/// 主机扩展方法测试
/// </summary>
/// <remarks>
/// <c>InitializeAsync</c> 是通用主机与曦寒应用之间唯一的粘合点，它做三件事：
/// 把主机的服务提供器交给应用、把应用的关闭挂到主机的 ApplicationStopping 上、把应用的释放挂到 ApplicationStopped 上。
/// 第二件最容易被漏掉又最难在集成环境里发现——漏了就表现为「进程能退出但模块的关闭钩子从没跑过」，
/// 因此这里直接触发主机的停止信号来验证挂载确实生效。
/// 环境名固定为 Production，避免开发机上的 DOTNET_ENVIRONMENT 让主机打开作用域校验，导致用例在不同机器上表现不一致。
/// </remarks>
public class HostExtensionsTests
{
    /// <summary>
    /// 初始化把主机的服务提供器交给应用并跑完模块生命周期
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InitializeAsync_HandsHostServicesToApplicationAndRunsModuleLifecycle()
    {
        var builder = CreateHostBuilder();
        var application = builder.Services.AddApplication<RecordingTestModule>();

        using var host = builder.Build();

        await host.InitializeAsync();

        Assert.Same(host.Services, application.ServiceProvider);

        var recorder = host.Services.GetRequiredService<ModuleLifecycleRecorder>();
        Assert.Contains("OnPreApplicationInitialization", recorder.Steps);
        Assert.Contains("OnApplicationInitialization", recorder.Steps);
        Assert.Contains("OnPostApplicationInitialization", recorder.Steps);
    }

    /// <summary>
    /// 初始化之后主机的停止信号会触发模块关闭
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InitializeAsync_HooksApplicationStoppingToShutdown()
    {
        var builder = CreateHostBuilder();
        builder.Services.AddApplication<RecordingTestModule>();

        using var host = builder.Build();
        await host.InitializeAsync();

        var recorder = host.Services.GetRequiredService<ModuleLifecycleRecorder>();
        Assert.DoesNotContain("OnApplicationShutdown", recorder.Steps);

        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        Assert.Contains("OnApplicationShutdown", recorder.Steps);
    }

    /// <summary>
    /// 主机里没有登记曦寒应用时初始化直接失败
    /// </summary>
    /// <remarks>
    /// 这是「忘了调 AddApplication 就调 InitializeAsync」的典型误用，
    /// 必须当场失败而不是安静地什么都不做，否则模块生命周期全部落空却没有任何信号。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public async Task InitializeAsync_WhenApplicationNotRegistered_Throws()
    {
        var builder = CreateHostBuilder();

        using var host = builder.Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.InitializeAsync());
    }

    /// <summary>
    /// 模块在服务配置阶段登记的服务可以从主机容器里解析出来
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InitializeAsync_ResolvesServicesRegisteredByModuleFromHost()
    {
        var builder = CreateHostBuilder();
        builder.Services.AddApplication<RecordingTestModule>();

        using var host = builder.Build();
        await host.InitializeAsync();

        Assert.Equal("marker", host.Services.GetRequiredService<ModuleMarkerService>().Value);
    }

    /// <summary>
    /// 建一个环境名固定为生产的主机构建器
    /// </summary>
    /// <returns>主机应用构建器</returns>
    private static HostApplicationBuilder CreateHostBuilder()
    {
        return Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production
        });
    }
}
