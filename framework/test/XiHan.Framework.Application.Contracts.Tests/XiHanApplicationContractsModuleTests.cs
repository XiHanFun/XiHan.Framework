// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Domain.Shared;

namespace XiHan.Framework.Application.Contracts.Tests;

/// <summary>
/// 应用层契约模块测试
/// </summary>
/// <remarks>
/// 契约模块本身不注册任何服务——它存在的意义只有两个：把 Domain.Shared 模块拉进依赖图，
/// 以及给上层模块一个可 DependsOn 的锚点。所以这里锁定的是「依赖声明」与「零注册」，
/// 一旦有人往 ConfigureServices 里塞注册，本用例会立刻暴露契约层被写脏。
/// </remarks>
public class XiHanApplicationContractsModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，才能被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_InheritsXiHanModule()
    {
        Assert.True(typeof(XiHanModule).IsAssignableFrom(typeof(XiHanApplicationContractsModule)));
        Assert.False(typeof(XiHanApplicationContractsModule).IsAbstract);
    }

    /// <summary>
    /// 模块显式依赖领域共享模块
    /// </summary>
    /// <remarks>
    /// 契约里的分页出入参来自 Domain.Shared，缺了这条依赖会在运行期变成「类型能引用、模块没加载」的半初始化状态。
    /// </remarks>
    [Fact]
    public void Module_DependsOnDomainSharedModule()
    {
        var dependedTypes = typeof(XiHanApplicationContractsModule)
            .GetCustomAttributes<DependsOnAttribute>(false)
            .SelectMany(attribute => attribute.GetDependedTypes())
            .ToArray();

        Assert.Contains(typeof(XiHanDomainSharedModule), dependedTypes);
    }

    /// <summary>
    /// 模块只声明领域共享模块这一条依赖
    /// </summary>
    [Fact]
    public void Module_DeclaresSingleDependency()
    {
        var dependedTypes = typeof(XiHanApplicationContractsModule)
            .GetCustomAttributes<DependsOnAttribute>(false)
            .SelectMany(attribute => attribute.GetDependedTypes())
            .ToArray();

        Assert.Single(dependedTypes);
    }

    /// <summary>
    /// 配置服务不向容器注册任何东西：契约层没有实现可注册
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersNothing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var context = new ServiceConfigurationContext(services);
        var countBefore = services.Count;

        new XiHanApplicationContractsModule().ConfigureServices(context);

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 配置服务不改写上下文的共享条目
    /// </summary>
    [Fact]
    public void ConfigureServices_LeavesContextItemsUntouched()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var context = new ServiceConfigurationContext(services);

        new XiHanApplicationContractsModule().ConfigureServices(context);

        Assert.Empty(context.Items);
        Assert.Same(services, context.Services);
    }

    /// <summary>
    /// 模块不跳过自动服务注册，沿用框架默认装配流程
    /// </summary>
    [Fact]
    public void Module_DoesNotOptOutOfAutoServiceRegistration()
    {
        var property = typeof(XiHanModule).GetProperty(
            "SkipAutoServiceRegistration",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotNull(property);
        Assert.False((bool)property!.GetValue(new XiHanApplicationContractsModule())!);
    }
}
