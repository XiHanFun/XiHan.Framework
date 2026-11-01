// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 在引擎所在的调用里执行一个活动（执行、恢复或补偿）。
/// </summary>
/// <remarks>
/// 活动与引擎同一服务作用域、同一调用上下文、同一事务：活动与结果处理器的写入随引擎的写入一起提交或回滚。抛出的异常翻译为 <see cref="WorkflowErrors.ActivityFailed"/>；取消以调用的取消令牌为准，原样抛出。
/// </remarks>
internal sealed class ActivityExecutor
{
    /// <summary>
    /// 执行范围：取当前调用。
    /// </summary>
    private readonly WorkflowScope _scope;

    /// <summary>
    /// 引擎所在的服务作用域：活动从这里解析。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 活动登记表。
    /// </summary>
    private readonly ActivityRegistry _registry;

    /// <summary>
    /// 求值器。
    /// </summary>
    private readonly IWorkflowExpressionEvaluator _evaluator;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建。
    /// </summary>
    public ActivityExecutor(WorkflowScope scope, IServiceProvider services, ActivityRegistry registry, IWorkflowExpressionEvaluator evaluator, TimeProvider clock)
    {
        _scope = scope;
        _services = services;
        _registry = registry;
        _evaluator = evaluator;
        _clock = clock;
    }

    /// <summary>
    /// 执行或恢复（给了书签即恢复）。
    /// </summary>
    /// <remarks>
    /// 不实现 <see cref="IResumableActivity"/> 的活动：超时书签即故障，其他书签以输入作输出完成。
    /// </remarks>
    public ValueTask<Result<ActivityOutcome>> RunAsync(WorkflowSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance, WorkflowBookmark? bookmark, JsonObject? inputs)
        => InvokeAsync(session, node, nodeInstance, async (activity, context) =>
        {
            if (bookmark is null)
            {
                return await activity.ExecuteAsync(context, context.Invocation.CancellationToken).ConfigureAwait(false);
            }

            var resume = new ActivityResumeContext(context.Definition, context.Instance, context.Node, context.NodeInstance, context.Variables, context.Services, context.Invocation, context.Evaluator, context.Clock, bookmark, inputs ?? []);
            if (activity is IResumableActivity resumable)
            {
                return await resumable.ResumeAsync(resume, context.Invocation.CancellationToken).ConfigureAwait(false);
            }

            if (resume.IsTimeout)
            {
                return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", node.ActivityType.Value), ("reason", "The node timed out.")));
            }

            return ActivityOutcome.Complete(WorkflowJson.CloneObject(resume.Inputs));
        });

    /// <summary>
    /// 补偿。
    /// </summary>
    /// <remarks>
    /// 活动不可补偿时返回成功（不做事）。
    /// </remarks>
    public async ValueTask<Result> CompensateAsync(WorkflowSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance)
    {
        var result = await InvokeAsync(session, node, nodeInstance, async (activity, context) =>
        {
            if (activity is ICompensableActivity compensable)
            {
                var compensated = await compensable.CompensateAsync(context, context.Invocation.CancellationToken).ConfigureAwait(false);
                return compensated.IsSuccess ? ActivityOutcome.Complete() : compensated.Error!;
            }

            return ActivityOutcome.Complete();
        }).ConfigureAwait(false);
        return result.ToResult();
    }

    /// <summary>
    /// 解析活动、建上下文、调用 <paramref name="body"/>。
    /// </summary>
    private async ValueTask<Result<ActivityOutcome>> InvokeAsync(WorkflowSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance, Func<IActivity, ActivityContext, ValueTask<Result<ActivityOutcome>>> body)
    {
        if (!_registry.Find(node.ActivityType).TryGetValue(out var definition))
        {
            return Error.NotFound(WorkflowErrors.TypeNotRegistered, WorkflowMessages.ActivityNotRegistered, MessageArguments.Of(("type", node.ActivityType.Value)));
        }

        // 引擎的每个操作都在一次调用里执行（调用方的，或引擎自己开的），活动沿用它：同一作用域、同一事务、同一租户
        if (!_scope.Invocation.TryGetValue(out var invocation))
        {
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", node.ActivityType.Value), ("reason", "The activity has no invocation to run in.")));
        }

        try
        {
            var activity = (IActivity)_services.GetRequiredService(definition.ActivityClass);
            var context = new ActivityContext(session.Definition, session.Instance, node, nodeInstance, session.Variables, _services, invocation, _evaluator, _clock);
            return await body(activity, context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (invocation.CancellationToken.IsCancellationRequested)
        {
            // 调用被取消：整个操作随调用回滚，不把取消记成节点故障
            throw;
        }
        catch (Exception exception)
        {
            // 活动抛出即节点故障；它在抛出之前做的写入留在同一事务里，不单独回滚
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", node.ActivityType.Value), ("reason", exception.Message)));
        }
    }
}
