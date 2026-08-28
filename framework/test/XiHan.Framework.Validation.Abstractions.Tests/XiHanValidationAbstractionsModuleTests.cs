// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Validation.Abstractions.Tests;

/// <summary>
/// 曦寒数据校验抽象模块测试
/// </summary>
/// <remarks>
/// 抽象模块本身不注册任何服务，能被外部观察到的行为只有三类：
/// 一是它被模块加载器识别为合法模块（类型形状 + 依赖声明 + 程序集集合）；
/// 二是 <c>ConfigureServices</c> 会向服务集合索取 <see cref="IConfiguration"/>，
/// 索取不到时直接抛 <see cref="XiHanException"/>，这构成一条对宿主的硬性前置要求；
/// 三是除此之外不产生任何副作用。断言就按这三类组织。
/// </remarks>
public class XiHanValidationAbstractionsModuleTests
{
    /// <summary>
    /// 模块类型能被模块加载器识别为合法的曦寒模块
    /// </summary>
    [Fact]
    public void Type_IsRecognizedAsXiHanModule()
    {
        var module = new XiHanValidationAbstractionsModule();

        Assert.True(XiHanModuleHelper.IsXiHanModule(typeof(XiHanValidationAbstractionsModule)));
        Assert.IsAssignableFrom<XiHanModule>(module);
        Assert.IsAssignableFrom<IXiHanModule>(module);
    }

    /// <summary>
    /// 模块提供公开无参构造函数供加载器反射实例化
    /// </summary>
    [Fact]
    public void Type_HasPublicParameterlessConstructor()
    {
        var constructor = typeof(XiHanValidationAbstractionsModule).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(constructor);
        Assert.True(constructor!.IsPublic);
    }

    /// <summary>
    /// 抽象模块不声明任何模块依赖
    /// </summary>
    /// <remarks>
    /// 抽象包只提供契约，挂上 DependsOn 会把实现层的依赖倒灌进所有引用方，这里把「零依赖」固定下来。
    /// </remarks>
    [Fact]
    public void Module_DeclaresNoModuleDependencies()
    {
        var dependencies = XiHanModuleHelper.FindDependedModuleTypes(typeof(XiHanValidationAbstractionsModule));

        Assert.Empty(dependencies);
    }

    /// <summary>
    /// 模块参与约定注册的程序集只有自身所在程序集
    /// </summary>
    [Fact]
    public void Module_ContributesOnlyItsOwnAssembly()
    {
        var assemblies = XiHanModuleHelper.GetAllAssemblies(typeof(XiHanValidationAbstractionsModule));

        var assembly = Assert.Single(assemblies);
        Assert.Equal("XiHan.Framework.Validation.Abstractions", assembly.GetName().Name);
    }

    /// <summary>
    /// 模块只重写了服务配置这一个生命周期钩子
    /// </summary>
    [Fact]
    public void Module_OverridesOnlyConfigureServicesHook()
    {
        var declaredMethods = typeof(XiHanValidationAbstractionsModule)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(new[] { nameof(XiHanValidationAbstractionsModule.ConfigureServices) }, declaredMethods);
    }

    /// <summary>
    /// 服务集合中存在配置时服务配置正常返回且不注册任何服务
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenConfigurationRegistered_RegistersNothing()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var countBefore = services.Count;

        new XiHanValidationAbstractionsModule().ConfigureServices(context);

        Assert.Equal(countBefore, services.Count);
        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 重复执行服务配置不会产生重复注册
    /// </summary>
    [Fact]
    public void ConfigureServices_CalledTwice_StaysIdempotent()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanValidationAbstractionsModule();
        var countBefore = services.Count;

        module.ConfigureServices(context);
        module.ConfigureServices(context);

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 服务集合中没有配置时服务配置抛出框架异常
    /// </summary>
    /// <remarks>
    /// 这条断言的价值在于把「宿主必须先把 IConfiguration 放进服务集合」这条隐式前置条件显式化：
    /// 模块本身并不使用配置，却会因为拿不到配置而让整个模块装配流程中断。
    /// </remarks>
    [Fact]
    public void ConfigureServices_WhenConfigurationMissing_ThrowsXiHanException()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanValidationAbstractionsModule();

        var exception = Assert.Throws<XiHanException>(() => module.ConfigureServices(context));

        Assert.Contains("在服务集合中找不到", exception.Message);
    }

    /// <summary>
    /// 异步服务配置在配置就绪时同样不注册任何服务
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_WhenConfigurationRegistered_RegistersNothing()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var countBefore = services.Count;

        await new XiHanValidationAbstractionsModule().ConfigureServicesAsync(context);

        Assert.Equal(countBefore, services.Count);
        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 异步服务配置会把同步重写里的异常原样透出
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_WhenConfigurationMissing_ThrowsXiHanException()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanValidationAbstractionsModule();

        var exception = await Assert.ThrowsAsync<XiHanException>(() => module.ConfigureServicesAsync(context));

        Assert.Contains("在服务集合中找不到", exception.Message);
    }

    /// <summary>
    /// 服务配置前后两个钩子都是空实现，且不要求配置就绪
    /// </summary>
    /// <remarks>
    /// 这里刻意不注册配置：前后置钩子若被误加上取配置的逻辑，这条用例会立刻变红。
    /// </remarks>
    [Fact]
    public void PreAndPostConfigureServices_AreNoOps()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanValidationAbstractionsModule();

        module.PreConfigureServices(context);
        module.PostConfigureServices(context);

        Assert.Empty(services);
        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 服务配置不会改写上下文里的共享条目
    /// </summary>
    [Fact]
    public void ConfigureServices_DoesNotTouchSharedContextItems()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        context["前置模块写入的标记"] = "原值";

        new XiHanValidationAbstractionsModule().ConfigureServices(context);

        Assert.Single(context.Items);
        Assert.Equal("原值", context["前置模块写入的标记"] as string);
    }

    /// <summary>
    /// 构造一个已放入空配置的服务集合
    /// </summary>
    /// <returns>服务集合</returns>
    private static ServiceCollection CreateServicesWithConfiguration()
    {
        var services = new ServiceCollection();
        services.ReplaceConfiguration(new ConfigurationBuilder().Build());
        return services;
    }
}
