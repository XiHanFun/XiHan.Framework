// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 在新作用域、新调用上下文里经完整内核管道执行一个活动（执行、恢复或补偿）：关联 ID = 实例 ID，租户 = 实例租户；异常翻译为 <see cref="WorkflowErrors.ActivityFailed"/>。
/// </summary>
internal sealed class ActivityExecutor
{
    /// <summary>
    /// 根服务提供者。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 应用。
    /// </summary>
    private readonly App _app;

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
    public ActivityExecutor(IServiceProvider services, App app, ActivityRegistry registry, IWorkflowExpressionEvaluator evaluator, TimeProvider clock)
    {
        _services = services;
        _app = app;
        _registry = registry;
        _evaluator = evaluator;
        _clock = clock;
    }

    /// <summary>
    /// 执行或恢复（给了书签即恢复）。不实现 <see cref="IResumableActivity"/> 的活动：超时书签即故障，其他书签以输入作输出完成。
    /// </summary>
    public ValueTask<Result<ActivityOutcome>> RunAsync(WorkflowSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance, WorkflowBookmark? bookmark, JsonObject? inputs, CancellationToken cancellationToken)
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
        }, cancellationToken);

    /// <summary>
    /// 补偿；活动不可补偿时返回成功（不做事）。
    /// </summary>
    public async ValueTask<Result> CompensateAsync(WorkflowSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance, CancellationToken cancellationToken)
    {
        var result = await InvokeAsync(session, node, nodeInstance, async (activity, context) =>
        {
            if (activity is ICompensableActivity compensable)
            {
                var compensated = await compensable.CompensateAsync(context, context.Invocation.CancellationToken).ConfigureAwait(false);
                return compensated.IsSuccess ? ActivityOutcome.Complete() : compensated.Error!;
            }

            return ActivityOutcome.Complete();
        }, cancellationToken).ConfigureAwait(false);
        return result.ToResult();
    }

    /// <summary>
    /// 开作用域、建上下文、经管道调用 <paramref name="body"/>。
    /// </summary>
    private async ValueTask<Result<ActivityOutcome>> InvokeAsync(WorkflowSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance, Func<IActivity, ActivityContext, ValueTask<Result<ActivityOutcome>>> body, CancellationToken cancellationToken)
    {
        if (!_registry.Find(node.ActivityType).TryGetValue(out var definition))
        {
            return Error.NotFound(WorkflowErrors.TypeNotRegistered, WorkflowMessages.ActivityNotRegistered, MessageArguments.Of(("type", node.ActivityType.Value)));
        }

        var instance = session.Instance;
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Correlation] = instance.Id };
        if (instance.TenantId is { Length: > 0 } tenant)
        {
            baggage[BaggageKeys.Tenant] = tenant;
        }

        Result<ActivityOutcome>? outcome = null;
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var context = App.CreateContext(scope.ServiceProvider, cancellationToken, baggage: baggage);
            var invoked = await _app.InvokeAsync(context, async ctx =>
            {
                var activity = (IActivity)ctx.Services.GetRequiredService(definition.ActivityClass);
                var activityContext = new ActivityContext(session.Definition, instance, node, nodeInstance, session.Variables, ctx.Services, ctx, _evaluator, _clock);
                try
                {
                    outcome = await body(activity, activityContext).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !ctx.CancellationToken.IsCancellationRequested)
                {
                    // 活动抛出即节点故障；管道的 outcome 也记失败，审计与追踪据此记录
                    outcome = Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", node.ActivityType.Value), ("reason", exception.Message)));
                }

                if (outcome is { IsFailure: true } failed)
                {
                    ctx.SetOutcome(Result.Failure(failed.Error!));
                }
            }).ConfigureAwait(false);

            // 管道在到达活动之前失败（如租户校验）时没有活动结果，以管道结果为故障
            return outcome ?? (invoked.IsFailure ? invoked.Error! : Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", node.ActivityType.Value), ("reason", "The pipeline did not reach the activity."))));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", node.ActivityType.Value), ("reason", exception.Message)));
        }
    }
}
