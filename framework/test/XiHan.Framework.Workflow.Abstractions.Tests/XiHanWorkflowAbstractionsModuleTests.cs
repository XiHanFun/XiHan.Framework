// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 工作流抽象模块测试
/// </summary>
/// <remarks>
/// 抽象包只定义契约、不提供实现，所以模块的正确行为恰恰是"什么都不注册"：
/// 一旦这里开始注册默认实现，依赖抽象包的项目就会被动拿到不想要的实现。
/// 因此断言的是"服务集合在装配前后完全不变"，而不是某个服务能解析出来。
/// </remarks>
public class XiHanWorkflowAbstractionsModuleTests
{
    /// <summary>
    /// 模块继承自曦寒模块基类
    /// </summary>
    [Fact]
    public void Type_DerivesFromXiHanModule()
    {
        Assert.True(typeof(XiHanModule).IsAssignableFrom(typeof(XiHanWorkflowAbstractionsModule)));
        Assert.True(typeof(IXiHanModule).IsAssignableFrom(typeof(XiHanWorkflowAbstractionsModule)));
        Assert.False(typeof(XiHanWorkflowAbstractionsModule).IsAbstract);
    }

    /// <summary>
    /// 模块有公共无参构造器，可被模块加载器实例化
    /// </summary>
    [Fact]
    public void Type_HasPublicParameterlessConstructor()
    {
        var constructor = typeof(XiHanWorkflowAbstractionsModule).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    /// <summary>
    /// 装配后服务集合无任何新增注册
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersNothing()
    {
        var services = CreateServicesWithConfiguration();
        var countBefore = services.Count;

        new XiHanWorkflowAbstractionsModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 异步装配入口与同步入口行为一致
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_BehavesSameAsSyncEntry()
    {
        var services = CreateServicesWithConfiguration();
        var countBefore = services.Count;

        await new XiHanWorkflowAbstractionsModule().ConfigureServicesAsync(new ServiceConfigurationContext(services));

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 装配不会替换或移除已有注册
    /// </summary>
    [Fact]
    public void ConfigureServices_KeepsExistingRegistrationsResolvable()
    {
        var services = CreateServicesWithConfiguration();
        services.AddSingleton(new WorkflowDefinition { Code = "leave" });

        new XiHanWorkflowAbstractionsModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IConfiguration>());
        Assert.Equal("leave", provider.GetRequiredService<WorkflowDefinition>().Code);
    }

    /// <summary>
    /// 构造带配置实例的服务集合
    /// </summary>
    /// <remarks>
    /// 模块装配阶段会从服务集合里取配置，缺失配置会直接抛框架异常，与工作流契约无关，
    /// 因此所有用例都先注册一个空配置根，把验证重点留在"是否注册服务"上。
    /// </remarks>
    /// <returns>服务集合</returns>
    private static IServiceCollection CreateServicesWithConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }
}
