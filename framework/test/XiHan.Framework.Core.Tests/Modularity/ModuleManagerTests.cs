// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 模块管理器测试
/// </summary>
/// <remarks>
/// 管理器的遍历顺序是「外层贡献者、内层模块」：同一个生命周期阶段先跑完所有模块，才进入下一阶段，
/// 这正是 OnPreApplicationInitialization → OnApplicationInitialization → OnPostApplicationInitialization
/// 能跨模块成立的原因；关闭阶段模块顺序整体反转，保证后初始化的先释放。
/// 任一模块抛出都必须被包装成初始化/关闭异常，并在消息里带上模块与贡献者身份、保留原始异常。
/// </remarks>
public class ModuleManagerTests
{
    /// <summary>
    /// 初始化按阶段跨模块推进
    /// </summary>
    [Fact]
    public void InitializeModules_RunsEachStageAcrossAllModulesBeforeNextStage()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = BuildInitializationOptions();
        var manager = CreateManager(provider, options, DescribeFirst(calls), DescribeSecond(calls));

        manager.InitializeModules(new ApplicationInitializationContext(provider));

        Assert.Equal(6, calls.Count);
        Assert.Equal("first:Pre", calls[0]);
        Assert.Equal("second:Pre", calls[1]);
        Assert.Equal("first:Init", calls[2]);
        Assert.Equal("second:Init", calls[3]);
        Assert.Equal("first:Post", calls[4]);
        Assert.Equal("second:Post", calls[5]);
    }

    /// <summary>
    /// 异步初始化与同步初始化顺序一致
    /// </summary>
    [Fact]
    public async Task InitializeModulesAsync_KeepsSameStageOrder()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = BuildInitializationOptions();
        var manager = CreateManager(provider, options, DescribeFirst(calls), DescribeSecond(calls));

        await manager.InitializeModulesAsync(new ApplicationInitializationContext(provider));

        Assert.Equal(6, calls.Count);
        Assert.Equal("first:Pre", calls[0]);
        Assert.Equal("second:Post", calls[5]);
    }

    /// <summary>
    /// 关闭时模块顺序整体反转
    /// </summary>
    [Fact]
    public void ShutdownModules_ReversesModuleOrder()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = new XiHanModuleLifecycleOptions();
        options.Contributors.Add<OnApplicationShutdownModuleLifecycleContributor>();
        var manager = CreateManager(provider, options, DescribeFirst(calls), DescribeSecond(calls));

        manager.ShutdownModules(new ApplicationShutdownContext(provider));

        Assert.Equal(2, calls.Count);
        Assert.Equal("second:Shutdown", calls[0]);
        Assert.Equal("first:Shutdown", calls[1]);
    }

    /// <summary>
    /// 异步关闭与同步关闭顺序一致
    /// </summary>
    [Fact]
    public async Task ShutdownModulesAsync_ReversesModuleOrder()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = new XiHanModuleLifecycleOptions();
        options.Contributors.Add<OnApplicationShutdownModuleLifecycleContributor>();
        var manager = CreateManager(provider, options, DescribeFirst(calls), DescribeSecond(calls));

        await manager.ShutdownModulesAsync(new ApplicationShutdownContext(provider));

        Assert.Equal(2, calls.Count);
        Assert.Equal("second:Shutdown", calls[0]);
        Assert.Equal("first:Shutdown", calls[1]);
    }

    /// <summary>
    /// 未登记贡献者时什么也不做
    /// </summary>
    [Fact]
    public void InitializeModules_WhenNoContributor_DoesNothing()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var manager = CreateManager(provider, new XiHanModuleLifecycleOptions(), DescribeFirst(calls));

        manager.InitializeModules(new ApplicationInitializationContext(provider));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 初始化阶段抛出时包装为初始化异常并保留原始异常
    /// </summary>
    [Fact]
    public void InitializeModules_WhenContributorThrows_WrapsIntoInitializationException()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = new XiHanModuleLifecycleOptions();
        options.Contributors.Add<MmThrowingContributor>();
        var manager = CreateManager(provider, options, DescribeFirst(calls));

        var exception = Assert.Throws<InitializationException>(() => manager.InitializeModules(new ApplicationInitializationContext(provider)));

        Assert.Contains(typeof(MmFirstModule).AssemblyQualifiedName!, exception.Message);
        Assert.Contains(typeof(MmThrowingContributor).FullName!, exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    /// <summary>
    /// 异步初始化阶段抛出时同样包装为初始化异常
    /// </summary>
    [Fact]
    public async Task InitializeModulesAsync_WhenContributorThrows_WrapsIntoInitializationException()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = new XiHanModuleLifecycleOptions();
        options.Contributors.Add<MmThrowingContributor>();
        var manager = CreateManager(provider, options, DescribeFirst(calls));

        var exception = await Assert.ThrowsAsync<InitializationException>(
            () => manager.InitializeModulesAsync(new ApplicationInitializationContext(provider)));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    /// <summary>
    /// 关闭阶段抛出时包装为关闭异常
    /// </summary>
    [Fact]
    public void ShutdownModules_WhenContributorThrows_WrapsIntoShutdownException()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = new XiHanModuleLifecycleOptions();
        options.Contributors.Add<MmThrowingContributor>();
        var manager = CreateManager(provider, options, DescribeFirst(calls));

        var exception = Assert.Throws<ShutdownException>(() => manager.ShutdownModules(new ApplicationShutdownContext(provider)));

        Assert.Contains(typeof(MmThrowingContributor).FullName!, exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    /// <summary>
    /// 异步关闭阶段抛出时同样包装为关闭异常
    /// </summary>
    [Fact]
    public async Task ShutdownModulesAsync_WhenContributorThrows_WrapsIntoShutdownException()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = new XiHanModuleLifecycleOptions();
        options.Contributors.Add<MmThrowingContributor>();
        var manager = CreateManager(provider, options, DescribeFirst(calls));

        var exception = await Assert.ThrowsAsync<ShutdownException>(
            () => manager.ShutdownModulesAsync(new ApplicationShutdownContext(provider)));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    /// <summary>
    /// 只实现部分钩子的模块不会被无关贡献者打扰
    /// </summary>
    [Fact]
    public void InitializeModules_WhenModuleDoesNotImplementHook_SkipsItSilently()
    {
        List<string> calls = [];
        using var provider = BuildProvider();
        var options = BuildInitializationOptions();
        var bare = new MmBareModule();
        var manager = CreateManager(provider, options, new XiHanModuleDescriptor(typeof(MmBareModule), bare, false), DescribeFirst(calls));

        manager.InitializeModules(new ApplicationInitializationContext(provider));

        Assert.Equal(3, calls.Count);
        Assert.Equal("first:Pre", calls[0]);
    }

    /// <summary>
    /// 构建注册了全部生命周期贡献者的服务提供器
    /// </summary>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<OnPreApplicationInitializationModuleLifecycleContributor>();
        services.AddTransient<OnApplicationInitializationModuleLifecycleContributor>();
        services.AddTransient<OnPostApplicationInitializationModuleLifecycleContributor>();
        services.AddTransient<OnApplicationShutdownModuleLifecycleContributor>();
        services.AddTransient<MmThrowingContributor>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 构建按前置、初始化、后置排列的生命周期选项
    /// </summary>
    /// <returns>生命周期选项</returns>
    private static XiHanModuleLifecycleOptions BuildInitializationOptions()
    {
        var options = new XiHanModuleLifecycleOptions();
        options.Contributors.Add<OnPreApplicationInitializationModuleLifecycleContributor>();
        options.Contributors.Add<OnApplicationInitializationModuleLifecycleContributor>();
        options.Contributors.Add<OnPostApplicationInitializationModuleLifecycleContributor>();
        return options;
    }

    /// <summary>
    /// 构建模块管理器
    /// </summary>
    /// <param name="provider">服务提供器</param>
    /// <param name="options">生命周期选项</param>
    /// <param name="modules">模块描述器</param>
    /// <returns>模块管理器</returns>
    private static ModuleManager CreateManager(IServiceProvider provider, XiHanModuleLifecycleOptions options, params IModuleDescriptor[] modules)
    {
        return new ModuleManager(new MmModuleContainer(modules), NullLogger<ModuleManager>.Instance,
            new MmLifecycleOptionsWrapper(options), provider);
    }

    /// <summary>
    /// 构建第一个记录型模块的描述器
    /// </summary>
    /// <param name="calls">调用记录</param>
    /// <returns>模块描述器</returns>
    private static IModuleDescriptor DescribeFirst(List<string> calls)
    {
        return new XiHanModuleDescriptor(typeof(MmFirstModule), new MmFirstModule(calls), false);
    }

    /// <summary>
    /// 构建第二个记录型模块的描述器
    /// </summary>
    /// <param name="calls">调用记录</param>
    /// <returns>模块描述器</returns>
    private static IModuleDescriptor DescribeSecond(List<string> calls)
    {
        return new XiHanModuleDescriptor(typeof(MmSecondModule), new MmSecondModule(calls), false);
    }
}

/// <summary>
/// 固定内容的模块容器
/// </summary>
internal sealed class MmModuleContainer : IModuleContainer
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="modules">模块描述器</param>
    public MmModuleContainer(params IModuleDescriptor[] modules)
    {
        Modules = modules;
    }

    /// <summary>
    /// 模块列表
    /// </summary>
    public IReadOnlyList<IModuleDescriptor> Modules { get; }
}

/// <summary>
/// 直接返回既有选项实例的选项包装
/// </summary>
internal sealed class MmLifecycleOptionsWrapper : IOptions<XiHanModuleLifecycleOptions>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">选项实例</param>
    public MmLifecycleOptionsWrapper(XiHanModuleLifecycleOptions value)
    {
        Value = value;
    }

    /// <summary>
    /// 选项实例
    /// </summary>
    public XiHanModuleLifecycleOptions Value { get; }
}

/// <summary>
/// 记录生命周期调用的第一个模块
/// </summary>
internal sealed class MmFirstModule : XiHanModule
{
    private readonly List<string> _calls;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="calls">调用记录</param>
    public MmFirstModule(List<string> calls)
    {
        _calls = calls;
    }

    /// <summary>
    /// 应用初始化前
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        _calls.Add("first:Pre");
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        _calls.Add("first:Init");
    }

    /// <summary>
    /// 应用初始化后
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
        _calls.Add("first:Post");
    }

    /// <summary>
    /// 应用关闭
    /// </summary>
    /// <param name="context">应用关闭上下文</param>
    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _calls.Add("first:Shutdown");
    }
}

/// <summary>
/// 记录生命周期调用的第二个模块
/// </summary>
internal sealed class MmSecondModule : XiHanModule
{
    private readonly List<string> _calls;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="calls">调用记录</param>
    public MmSecondModule(List<string> calls)
    {
        _calls = calls;
    }

    /// <summary>
    /// 应用初始化前
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        _calls.Add("second:Pre");
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        _calls.Add("second:Init");
    }

    /// <summary>
    /// 应用初始化后
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
        _calls.Add("second:Post");
    }

    /// <summary>
    /// 应用关闭
    /// </summary>
    /// <param name="context">应用关闭上下文</param>
    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _calls.Add("second:Shutdown");
    }
}

/// <summary>
/// 只实现模块基本契约、不参与应用生命周期的模块
/// </summary>
internal sealed class MmBareModule : IXiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public void ConfigureServices(ServiceConfigurationContext context)
    {
    }

    /// <summary>
    /// 服务配置，异步
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    /// <returns>任务</returns>
    public Task ConfigureServicesAsync(ServiceConfigurationContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 初始化与关闭都会抛出的贡献者
/// </summary>
internal sealed class MmThrowingContributor : ModuleLifecycleContributorBase
{
    /// <summary>
    /// 初始化，异步
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    /// <param name="module">模块</param>
    /// <returns>任务</returns>
    public override Task InitializeAsync(ApplicationInitializationContext context, IXiHanModule module)
    {
        throw new InvalidOperationException("模块初始化失败");
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    /// <param name="module">模块</param>
    public override void Initialize(ApplicationInitializationContext context, IXiHanModule module)
    {
        throw new InvalidOperationException("模块初始化失败");
    }

    /// <summary>
    /// 关闭，异步
    /// </summary>
    /// <param name="context">应用关闭上下文</param>
    /// <param name="module">模块</param>
    /// <returns>任务</returns>
    public override Task ShutdownAsync(ApplicationShutdownContext context, IXiHanModule module)
    {
        throw new InvalidOperationException("模块关闭失败");
    }

    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context">应用关闭上下文</param>
    /// <param name="module">模块</param>
    public override void Shutdown(ApplicationShutdownContext context, IXiHanModule module)
    {
        throw new InvalidOperationException("模块关闭失败");
    }
}
