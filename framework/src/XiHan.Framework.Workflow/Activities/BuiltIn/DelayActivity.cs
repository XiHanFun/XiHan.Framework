// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Timing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 延时活动（挂起等待指定时长，由定时器 Worker 到期恢复，不占用线程）
/// </summary>
/// <remarks>
/// 节点属性：<c>DurationSeconds</c>（等待秒数，数值或求值为数值的表达式）。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.Delay, DisplayName = "延时", Category = "流程控制")]
public class DelayActivity : WorkflowActivityBase, IResumableWorkflowActivity
{
    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        double durationSeconds;

        // 先归一化：JSON 反序列化的定义中属性值是 JsonElement，直接模式匹配 string 会失配
        var raw = WorkflowValueConverter.Normalize(GetProperty<object>(context, "DurationSeconds"));
        if (raw is string expression)
        {
            var evaluator = GetEvaluator(context);
            var value = await evaluator.EvaluateAsync(expression, context.Variables.AsReadOnly, context.CancellationToken);
            durationSeconds = Convert.ToDouble(WorkflowValueConverter.ConvertTo<decimal>(value));
        }
        else
        {
            durationSeconds = Convert.ToDouble(WorkflowValueConverter.ConvertTo<decimal>(raw));
        }

        if (durationSeconds <= 0)
        {
            return ActivityExecutionResult.Fault($"延时节点 {context.Node.Id} 的 DurationSeconds 必须大于 0");
        }

        var clock = context.ServiceProvider.GetRequiredService<IClock>();
        return ActivityExecutionResult.Suspend(new WorkflowBookmarkRequest
        {
            Kind = WorkflowBookmarkKinds.Timer,
            DueTime = clock.Now.AddSeconds(durationSeconds)
        });
    }

    /// <inheritdoc />
    public Task<ActivityExecutionResult> ResumeAsync(ActivityResumeContext context)
    {
        // 节点超时不是正常到期，按故障处理，避免超时被误判为延时结束提前放行
        return Task.FromResult(context.Bookmark.Kind == WorkflowBookmarkKinds.NodeTimeout
            ? ActivityExecutionResult.Fault($"延时节点 {context.Node.Id} 等待超时")
            : ActivityExecutionResult.Complete());
    }
}
