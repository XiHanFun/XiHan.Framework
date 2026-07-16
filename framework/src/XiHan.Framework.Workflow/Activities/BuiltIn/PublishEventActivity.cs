#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PublishEventActivity
// Guid:6f93a2d0-58e1-4c74-b6a9-27d05f81c3e6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:12:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Events;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 发布事件活动（向事件总线发布自定义业务事件 <see cref="WorkflowCustomEventData"/>）
/// </summary>
/// <remarks>
/// 节点属性：<c>EventName</c>（事件名称，支持模板）；<c>Payload</c>（载荷字典，字符串值支持模板）。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.PublishEvent, DisplayName = "发布事件", Category = "事件")]
public class PublishEventActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var eventName = await GetTemplatedStringAsync(context, "EventName");
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return ActivityExecutionResult.Fault($"发布事件节点 {context.Node.Id} 未配置 EventName");
        }

        var payload = new Dictionary<string, object?>();
        var rawPayload = GetProperty<Dictionary<string, object?>>(context, "Payload");
        if (rawPayload is not null)
        {
            var evaluator = GetEvaluator(context);
            foreach (var pair in rawPayload)
            {
                // 先归一化：JSON 反序列化的定义中字典值是 JsonElement，直接模式匹配 string 会漏掉模板渲染
                payload[pair.Key] = WorkflowValueConverter.Normalize(pair.Value) is string template
                    ? await evaluator.RenderTemplateAsync(template, context.Variables.AsReadOnly, context.CancellationToken)
                    : WorkflowValueConverter.Normalize(pair.Value);
            }
        }

        var publisher = context.ServiceProvider.GetRequiredService<IWorkflowEventPublisher>();
        await publisher.PublishAsync(new WorkflowCustomEventData(
            eventName, context.Instance.Id, context.Instance.CorrelationId, payload));

        return ActivityExecutionResult.Complete();
    }
}
