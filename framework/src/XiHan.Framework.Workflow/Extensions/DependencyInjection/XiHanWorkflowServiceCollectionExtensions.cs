// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Script.Core;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Abstractions.UserTasks;
using XiHan.Framework.Workflow.Activities;
using XiHan.Framework.Workflow.Activities.BuiltIn;
using XiHan.Framework.Workflow.Definitions;
using XiHan.Framework.Workflow.Engine;
using XiHan.Framework.Workflow.Events;
using XiHan.Framework.Workflow.Expressions;
using XiHan.Framework.Workflow.Options;
using XiHan.Framework.Workflow.Stores;
using XiHan.Framework.Workflow.UserTasks;
using XiHan.Framework.Workflow.Workers;

namespace XiHan.Framework.Workflow.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架工作流服务集合扩展方法
/// </summary>
public static class XiHanWorkflowServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒框架工作流服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWorkflow(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 选项
        services.Configure<XiHanWorkflowOptions>(configuration.GetSection(XiHanWorkflowOptions.SectionName));
        services.Configure<XiHanWorkflowWorkerOptions>(configuration.GetSection(XiHanWorkflowWorkerOptions.SectionName));

        // 核心服务（可被应用侧替换：如替换存储为数据库/Redis 实现、替换表达式求值器为脚本引擎实现）
        services.TryAddSingleton<IWorkflowExpressionEvaluator, WorkflowExpressionEvaluator>();
        services.TryAddSingleton<IWorkflowActivityRegistry, WorkflowActivityRegistry>();
        services.TryAddSingleton<IWorkflowEventPublisher, LocalEventBusWorkflowEventPublisher>();
        services.TryAddSingleton<IWorkflowDefinitionStore, InMemoryWorkflowDefinitionStore>();
        services.TryAddSingleton<IWorkflowInstanceStore, InMemoryWorkflowInstanceStore>();
        services.TryAddSingleton<IWorkflowBookmarkStore, InMemoryWorkflowBookmarkStore>();
        services.TryAddTransient<IWorkflowEngine, WorkflowEngine>();
        services.TryAddTransient<IWorkflowDefinitionManager, WorkflowDefinitionManager>();
        services.TryAddTransient<IWorkflowUserTaskService, WorkflowUserTaskService>();
        services.TryAddTransient<WorkflowDefinitionValidator>();

        // 脚本活动依赖的脚本引擎（脚本模块未注册 DI，这里补默认注册）
        services.TryAddSingleton<IScriptEngine>(_ => new ScriptEngine());

        // HTTP 活动的命名客户端
        services.AddHttpClient(HttpRequestActivity.HttpClientName);

        // 定时器轮询 Worker
        services.AddHostedService<WorkflowTimerWorker>();

        // 内置活动
        RegisterBuiltInActivities(services);

        return services;
    }

    /// <summary>
    /// 注册自定义工作流活动
    /// </summary>
    /// <typeparam name="TActivity">活动类型（须标注 WorkflowActivityAttribute）</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWorkflowActivity<TActivity>(this IServiceCollection services)
        where TActivity : class, IWorkflowActivity
    {
        services.TryAddTransient<TActivity>();
        services.Configure<XiHanWorkflowOptions>(options => options.Activities.TryAdd<TActivity>());
        return services;
    }

    private static void RegisterBuiltInActivities(IServiceCollection services)
    {
        services.AddXiHanWorkflowActivity<StartActivity>();
        services.AddXiHanWorkflowActivity<EndActivity>();
        services.AddXiHanWorkflowActivity<TerminateActivity>();
        services.AddXiHanWorkflowActivity<FaultActivity>();
        services.AddXiHanWorkflowActivity<LogActivity>();
        services.AddXiHanWorkflowActivity<SetVariableActivity>();
        services.AddXiHanWorkflowActivity<DecisionActivity>();
        services.AddXiHanWorkflowActivity<ParallelGatewayActivity>();
        services.AddXiHanWorkflowActivity<JoinActivity>();
        services.AddXiHanWorkflowActivity<DelayActivity>();
        services.AddXiHanWorkflowActivity<WaitSignalActivity>();
        services.AddXiHanWorkflowActivity<UserTaskActivity>();
        services.AddXiHanWorkflowActivity<HttpRequestActivity>();
        services.AddXiHanWorkflowActivity<ScriptActivity>();
        services.AddXiHanWorkflowActivity<PublishEventActivity>();
        services.AddXiHanWorkflowActivity<SubWorkflowActivity>();
        services.AddXiHanWorkflowActivity<ForEachActivity>();
    }
}
