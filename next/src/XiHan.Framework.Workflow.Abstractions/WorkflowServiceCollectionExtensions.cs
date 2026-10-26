// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.ExternalTasks;
using XiHan.Framework.Workflow.Abstractions.Tasks;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 登记活动、指派规则 Provider、结果处理器，以及 Provider 特性装配引擎用的 <see cref="AddWorkflowEngine"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// 登记活动：作用域服务加登记表；同一类型名登记两个不同的类是编程错误。
    /// </summary>
    public static IServiceCollection AddActivity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActivity>(this IServiceCollection services, ActivityType type)
        where TActivity : class, IActivity
        => services.AddActivity<TActivity>(type, ActivityOutgoing.AllMatched, null);

    /// <summary>
    /// 登记一个活动并给出出边选择方式与面向设计器的描述。
    /// </summary>
    public static IServiceCollection AddActivity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TActivity>(this IServiceCollection services, ActivityType type, ActivityOutgoing outgoing = ActivityOutgoing.AllMatched, ActivityDescriptor? descriptor = null)
        where TActivity : class, IActivity
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<TActivity>();
        services.GetActivityRegistry().Add(new ActivityDefinition(type, typeof(TActivity), outgoing, descriptor));
        return services;
    }

    /// <summary>
    /// 登记指派规则 Provider（作用域，可枚举）。
    /// </summary>
    public static IServiceCollection AddAssigneeProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(this IServiceCollection services)
        where TProvider : class, IAssigneeProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAssigneeProvider, TProvider>());
        return services;
    }

    /// <summary>
    /// 登记结果处理器（作用域，可枚举）。
    /// </summary>
    public static IServiceCollection AddWorkflowOutcomeHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where THandler : class, IWorkflowOutcomeHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowOutcomeHandler, THandler>());
        return services;
    }

    /// <summary>
    /// 取（必要时创建）活动登记表。
    /// </summary>
    public static ActivityRegistry GetActivityRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 登记表以实例注册，多次调用返回同一对象，特性与应用可以共同填充
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(ActivityRegistry) && descriptor.ImplementationInstance is ActivityRegistry existing)
            {
                return existing;
            }
        }

        var registry = new ActivityRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    /// <summary>
    /// 装配引擎：选项、内置活动与指派规则、求值器、校验、引擎、定义目录、任务服务与轮询器。Provider 特性在登记存储与锁后调用；重复调用无害。
    /// </summary>
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services, WorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.TryAddSingleton(options);
        services.AddActivity<StartActivity>(ActivityTypes.Start, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.Start);
        services.AddActivity<EndActivity>(ActivityTypes.End, ActivityOutgoing.None, BuiltInActivityDescriptors.End);
        services.AddActivity<TerminateActivity>(ActivityTypes.Terminate, ActivityOutgoing.None, BuiltInActivityDescriptors.Terminate);
        services.AddActivity<SetVariableActivity>(ActivityTypes.SetVariable, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.SetVariable);
        services.AddActivity<DecisionActivity>(ActivityTypes.Decision, ActivityOutgoing.Exclusive, BuiltInActivityDescriptors.Decision);
        services.AddActivity<ParallelActivity>(ActivityTypes.Parallel, ActivityOutgoing.All, BuiltInActivityDescriptors.Parallel);
        services.AddActivity<JoinActivity>(ActivityTypes.Join, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.Join);
        services.AddActivity<DelayActivity>(ActivityTypes.Delay, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.Delay);
        services.AddActivity<UserTaskActivity>(ActivityTypes.UserTask, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.UserTask);
        services.AddActivity<PublishEventActivity>(ActivityTypes.PublishEvent, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.PublishEvent);
        services.AddActivity<WaitSignalActivity>(ActivityTypes.WaitSignal, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.WaitSignal);
        services.AddActivity<SubWorkflowActivity>(ActivityTypes.SubWorkflow, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.SubWorkflow);
        services.AddActivity<ForEachActivity>(ActivityTypes.ForEach, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.ForEach);
        services.AddActivity<LogActivity>(ActivityTypes.Log, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.Log);
        services.AddActivity<FaultActivity>(ActivityTypes.Fault, ActivityOutgoing.None, BuiltInActivityDescriptors.Fault);
        services.AddActivity<EventGatewayActivity>(ActivityTypes.EventGateway, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.EventGateway);
        services.AddActivity<CompensateActivity>(ActivityTypes.Compensate, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.Compensate);
        services.AddActivity<HttpActivity>(ActivityTypes.Http, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.Http);
        services.AddActivity<ExternalTaskActivity>(ActivityTypes.ExternalTask, ActivityOutgoing.AllMatched, BuiltInActivityDescriptors.ExternalTask);
        services.TryAddSingleton<WorkflowHttpClient>();
        services.AddAssigneeProvider<UserAssigneeProvider>();
        services.AddAssigneeProvider<StarterAssigneeProvider>();
        services.AddAssigneeProvider<ExpressionAssigneeProvider>();
        services.TryAddSingleton<IWorkflowExpressionEvaluator, WorkflowExpressionEvaluator>();
        services.TryAddScoped<AssigneeResolver>();
        services.TryAddScoped<WorkflowDefinitionValidator>();
        services.TryAddScoped<IWorkflowEngine, WorkflowEngine>();
        services.TryAddScoped<IWorkflowDefinitionCatalog, WorkflowDefinitionCatalog>();
        services.TryAddScoped<IWorkflowTaskService, WorkflowTaskService>();
        services.TryAddScoped<IWorkflowExternalTaskService, WorkflowExternalTaskService>();
        services.TryAddSingleton<DueBookmarkPoller>();
        return services;
    }
}
