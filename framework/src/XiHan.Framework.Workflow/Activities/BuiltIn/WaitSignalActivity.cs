#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WaitSignalActivity
// Guid:2d80b6f9-47ce-4a15-93d2-e61f80c54a97
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:11:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 等待信号活动（挂起等待外部信号发布，信号载荷合并进实例变量）
/// </summary>
/// <remarks>
/// 节点属性：<c>SignalName</c>（信号名称，支持模板）；
/// <c>AcceptAnyCorrelation</c>（为 true 时不限业务相关性，任意同名信号均可命中，默认 false 即仅命中与实例相关性一致的信号）。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.WaitSignal, DisplayName = "等待信号", Category = "事件")]
public class WaitSignalActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var signalName = await GetTemplatedStringAsync(context, "SignalName");
        if (string.IsNullOrWhiteSpace(signalName))
        {
            return ActivityExecutionResult.Fault($"等待信号节点 {context.Node.Id} 未配置 SignalName");
        }

        var acceptAnyCorrelation = GetProperty<bool>(context, "AcceptAnyCorrelation");

        // fail-closed：要求相关性匹配但实例未携带相关性时拒绝挂起，
        // 否则空相关性书签会命中任意定向信号，把别的业务实体的载荷合并进本实例
        if (!acceptAnyCorrelation && string.IsNullOrEmpty(context.Instance.CorrelationId))
        {
            return ActivityExecutionResult.Fault(
                $"等待信号节点 {context.Node.Id} 要求相关性匹配，但实例未设置 CorrelationId（如需接收任意信号请配置 AcceptAnyCorrelation=true）");
        }

        return ActivityExecutionResult.Suspend(new WorkflowBookmarkRequest
        {
            Kind = WorkflowBookmarkKinds.Signal,
            Key = signalName,
            CorrelationId = acceptAnyCorrelation ? null : context.Instance.CorrelationId
        });
    }
}
