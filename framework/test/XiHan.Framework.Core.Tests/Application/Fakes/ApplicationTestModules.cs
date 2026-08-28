// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Application.Fakes;

/// <summary>
/// 按发生顺序记录模块生命周期步骤的记录器
/// </summary>
/// <remarks>
/// 刻意不做成静态字段：模块实例由 <see cref="Activator"/> 创建，无法从外部注入，
/// 但模块可以把记录器放进它自己那个应用的服务集合里，从而做到「一个应用一份记录」，
/// 这样同一个模块类型被多个测试并行使用也不会互相污染。
/// </remarks>
public sealed class ModuleLifecycleRecorder
{
    private readonly object _gate = new();
    private readonly List<string> _steps = [];

    /// <summary>
    /// 按发生顺序返回已记录的步骤快照
    /// </summary>
    public IReadOnlyList<string> Steps
    {
        get
        {
            lock (_gate)
            {
                return [.. _steps];
            }
        }
    }

    /// <summary>
    /// 记录一个步骤
    /// </summary>
    /// <param name="step">步骤名称</param>
    public void Record(string step)
    {
        lock (_gate)
        {
            _steps.Add(step);
        }
    }
}

/// <summary>
/// 模块记录辅助方法
/// </summary>
public static class ModuleRecordingHelper
{
    /// <summary>
    /// 从服务集合取记录器，没有则创建并登记为单例实例
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>记录器</returns>
    public static ModuleLifecycleRecorder GetOrAddRecorder(IServiceCollection services)
    {
        var existing = services.GetSingletonInstanceOrNull<ModuleLifecycleRecorder>();
        if (existing is not null)
        {
            return existing;
        }

        ModuleLifecycleRecorder recorder = new();
        services.AddSingleton(recorder);
        return recorder;
    }
}

/// <summary>
/// 由约定注册自动登记的样例服务，用于验证模块的程序集自动注册开关
/// </summary>
public sealed class AutoRegisteredTestService : ISingletonDependency
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    public string Ping()
    {
        return "auto";
    }
}

/// <summary>
/// 由记录模块在服务配置阶段登记的标记服务
/// </summary>
public sealed class ModuleMarkerService
{
    /// <summary>
    /// 标记值
    /// </summary>
    public string Value { get; } = "marker";
}

/// <summary>
/// 什么都不做的最小模块，供只关心应用装配本身的用例使用
/// </summary>
public class EmptyTestModule : XiHanModule
{
}

/// <summary>
/// 全程记录服务配置与应用生命周期步骤的模块
/// </summary>
/// <remarks>
/// 只重写同步钩子：基类的异步钩子默认转调同步钩子，因此同步与异步两条路径都会被记录到，
/// 这正是需要断言的契约——异步入口不能悄悄绕过同步实现。
/// </remarks>
public class RecordingTestModule : XiHanModule
{
    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        ModuleRecordingHelper.GetOrAddRecorder(context.Services).Record("PreConfigureServices");
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ModuleRecordingHelper.GetOrAddRecorder(context.Services).Record("ConfigureServices");
        context.Services.AddSingleton<ModuleMarkerService>();
    }

    /// <summary>
    /// 服务配置后
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        ModuleRecordingHelper.GetOrAddRecorder(context.Services).Record("PostConfigureServices");
    }

    /// <summary>
    /// 程序初始化前
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.GetRequiredService<ModuleLifecycleRecorder>().Record("OnPreApplicationInitialization");
    }

    /// <summary>
    /// 程序初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.GetRequiredService<ModuleLifecycleRecorder>().Record("OnApplicationInitialization");
    }

    /// <summary>
    /// 程序初始化后
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.GetRequiredService<ModuleLifecycleRecorder>().Record("OnPostApplicationInitialization");
    }

    /// <summary>
    /// 程序关闭
    /// </summary>
    /// <param name="context">应用关闭上下文</param>
    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        context.ServiceProvider.GetRequiredService<ModuleLifecycleRecorder>().Record("OnApplicationShutdown");
    }
}

/// <summary>
/// 跳过程序集自动服务注册的模块
/// </summary>
public class SkipAutoRegistrationTestModule : XiHanModule
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SkipAutoRegistrationTestModule()
    {
        SkipAutoServiceRegistration = true;
    }
}

/// <summary>
/// 被依赖的模块
/// </summary>
public class DependencyTestModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ModuleRecordingHelper.GetOrAddRecorder(context.Services).Record("Dependency:ConfigureServices");
    }
}

/// <summary>
/// 依赖 <see cref="DependencyTestModule"/> 的启动模块
/// </summary>
[DependsOn(typeof(DependencyTestModule))]
public class DependentTestModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ModuleRecordingHelper.GetOrAddRecorder(context.Services).Record("Dependent:ConfigureServices");
    }
}

/// <summary>
/// 服务配置阶段抛错的模块
/// </summary>
public class FailingConfigureServicesTestModule : XiHanModule
{
    /// <summary>
    /// 抛错的服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    /// <exception cref="InvalidOperationException">固定抛出</exception>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        throw new InvalidOperationException("模块服务配置故意失败");
    }
}

/// <summary>
/// 应用初始化阶段抛错的模块
/// </summary>
public class FailingInitializationTestModule : XiHanModule
{
    /// <summary>
    /// 抛错的应用初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    /// <exception cref="InvalidOperationException">固定抛出</exception>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        throw new InvalidOperationException("模块初始化故意失败");
    }
}

/// <summary>
/// 应用关闭阶段抛错的模块
/// </summary>
public class FailingShutdownTestModule : XiHanModule
{
    /// <summary>
    /// 抛错的应用关闭
    /// </summary>
    /// <param name="context">应用关闭上下文</param>
    /// <exception cref="InvalidOperationException">固定抛出</exception>
    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        throw new InvalidOperationException("模块关闭故意失败");
    }
}
