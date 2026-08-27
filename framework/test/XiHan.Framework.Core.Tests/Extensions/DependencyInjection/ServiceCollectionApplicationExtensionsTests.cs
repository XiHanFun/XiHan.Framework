// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Tests.Application.Fakes;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务容器应用程序扩展方法测试
/// </summary>
/// <remarks>
/// 这组扩展是宿主接入曦寒的门面：四个 <c>AddApplication</c> 重载全部落到外部服务提供器形态上
/// （宿主自己有容器，框架不能再建一个），三个读取扩展则都走「装配期读单例实例」这条路，
/// 因此在没有应用的服务集合上必须直接抛错而不是返回空值。
/// </remarks>
public class ServiceCollectionApplicationExtensionsTests
{
    /// <summary>
    /// 泛型重载在给定服务集合上建出外部服务提供器形态的应用
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void AddApplication_Generic_BuildsExternalProviderApplicationOnGivenCollection()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = services.AddApplication<RecordingTestModule>();

        Assert.Same(services, app.Services);
        Assert.Equal(typeof(RecordingTestModule), app.StartupModuleType);
        Assert.Same(app, services.GetSingletonInstanceOrNull<IXiHanApplicationWithExternalServiceProvider>());
    }

    /// <summary>
    /// 按类型重载与泛型重载等价
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void AddApplication_ByType_IsEquivalentToGenericOverload()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = services.AddApplication(typeof(EmptyTestModule));

        Assert.Same(services, app.Services);
        Assert.Equal(typeof(EmptyTestModule), app.StartupModuleType);
    }

    /// <summary>
    /// 泛型异步重载把服务配置推迟到构造之后并只跑一遍
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task AddApplicationAsync_Generic_ConfiguresServicesExactlyOnce()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = await services.AddApplicationAsync<RecordingTestModule>();

        var recorder = services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        Assert.NotNull(recorder);
        Assert.Equal(new[] { "PreConfigureServices", "ConfigureServices", "PostConfigureServices" }, recorder!.Steps);
    }

    /// <summary>
    /// 按类型异步重载同样完成服务配置
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task AddApplicationAsync_ByType_ConfiguresServices()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = await services.AddApplicationAsync(typeof(RecordingTestModule));

        Assert.Same(services, app.Services);
        Assert.NotNull(services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>());
    }

    /// <summary>
    /// 选项委托被透传给应用创建过程
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void AddApplication_PassesOptionsActionThrough()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = services.AddApplication<EmptyTestModule>(options =>
        {
            options.ApplicationName = "曦寒门面测试";
            options.Environment = "Staging";
        });

        Assert.Equal("曦寒门面测试", app.ApplicationName);
        Assert.Equal("Staging", services.GetXiHanHostEnvironment().EnvironmentName);
    }

    /// <summary>
    /// 应用名与实例标识都从应用信息访问器读出
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetApplicationNameAndInstanceId_ReadFromApplicationInfoAccessor()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = services.AddApplication<EmptyTestModule>(options => options.ApplicationName = "曦寒门面测试");

        Assert.Equal("曦寒门面测试", services.GetApplicationName());
        Assert.Equal(app.InstanceId, services.GetApplicationInstanceId());
        Assert.True(Guid.TryParse(services.GetApplicationInstanceId(), out _));
    }

    /// <summary>
    /// 宿主环境从服务集合读出，且服务配置结束后已被兜底成生产环境
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetXiHanHostEnvironment_ReturnsRegisteredEnvironment()
    {
        IServiceCollection services = new ServiceCollection();

        using var app = services.AddApplication<EmptyTestModule>();

        var environment = services.GetXiHanHostEnvironment();

        Assert.NotNull(environment);
        Assert.Equal("Production", environment.EnvironmentName);
    }

    /// <summary>
    /// 服务集合里没有应用时三个读取扩展都直接抛错
    /// </summary>
    [Fact]
    public void ReadExtensions_WithoutApplication_Throw()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.GetApplicationName());
        Assert.Throws<InvalidOperationException>(() => services.GetApplicationInstanceId());
        Assert.Throws<InvalidOperationException>(() => services.GetXiHanHostEnvironment());
    }
}
