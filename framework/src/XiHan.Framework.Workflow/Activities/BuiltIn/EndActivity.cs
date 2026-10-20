// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Outcomes;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 结束活动
/// </summary>
/// <remarks>
/// 未声明 <c>Outcome</c>：消耗当前令牌；所有令牌与书签均结束后实例自动完成。
/// 声明了 <c>Outcome</c>（结果结束节点，如 approved / rejected）：先依次调用全部 <see cref="IWorkflowOutcomeHandler"/>
/// 把结论回写业务（任一抛出即本节点故障、实例进入故障态，重试时重新调用），全部成功后写入实例结果，
/// 并立即结束整个实例——其余并行分支的待办与等待点一并清理，用于"任一驳回即整单驳回"等场景。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.End, DisplayName = "结束", Category = "流程控制",
    OutgoingBehavior = ActivityOutgoingBehavior.None)]
public class EndActivity : WorkflowActivityBase
{
    /// <summary>
    /// 执行活动（不再流转出边；结果结束节点回写业务后结束整个实例）
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>执行结果</returns>
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var outcome = GetProperty<string>(context, "Outcome")?.Trim();
        if (string.IsNullOrEmpty(outcome))
        {
            return ActivityExecutionResult.Complete();
        }

        var outcomeContext = new WorkflowOutcomeContext
        {
            Definition = context.Definition,
            Instance = context.Instance,
            Node = context.Node,
            Outcome = outcome,
            Variables = context.Variables.AsReadOnly,
            ServiceProvider = context.ServiceProvider,
            CancellationToken = context.CancellationToken
        };

        // 处理器异常不在此吞掉：由引擎按节点故障处理，实例停在故障态等待重试
        foreach (var handler in context.ServiceProvider.GetServices<IWorkflowOutcomeHandler>())
        {
            await handler.HandleAsync(outcomeContext);
        }

        context.Instance.Outcome = outcome;
        return ActivityExecutionResult.EndInstance(outcome: outcome);
    }
}
