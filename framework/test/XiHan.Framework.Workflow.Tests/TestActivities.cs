#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TestActivities
// Guid:6a48d1e7-b053-4c92-8f60-24e75d19c3a8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 12:01:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Activities;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 补偿记录器（测试观察补偿执行顺序）
/// </summary>
public sealed class CompensationRecorder
{
    /// <summary>
    /// 已补偿的节点标识（按补偿顺序）
    /// </summary>
    public List<string> CompensatedNodeIds { get; } = [];
}

/// <summary>
/// 测试用不稳定活动（前 N-1 次尝试失败，第 N 次成功）
/// </summary>
/// <remarks>
/// 节点属性：<c>SucceedOnAttempt</c>（第几次尝试成功，默认 1）。
/// </remarks>
[WorkflowActivity("TestFlaky")]
public class FlakyActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var succeedOnAttempt = GetProperty<int?>(context, "SucceedOnAttempt") ?? 1;
        return Task.FromResult(context.NodeInstance.TryCount < succeedOnAttempt
            ? ActivityExecutionResult.Fault($"第 {context.NodeInstance.TryCount} 次尝试注定失败")
            : ActivityExecutionResult.Complete(new Dictionary<string, object?> { ["flakyDone"] = true }));
    }
}

/// <summary>
/// 测试用取消抛出活动（模拟活动执行途中被取消）
/// </summary>
[WorkflowActivity("TestCancellation")]
public class CancellationThrowingActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        throw new OperationCanceledException();
    }
}

/// <summary>
/// 测试用可补偿活动（完成时写变量，补偿时记录节点标识）
/// </summary>
[WorkflowActivity("TestCompensable")]
public class RecordingCompensableActivity : WorkflowActivityBase, ICompensableWorkflowActivity
{
    private readonly CompensationRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">补偿记录器</param>
    public RecordingCompensableActivity(CompensationRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <inheritdoc />
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }

    /// <inheritdoc />
    public Task CompensateAsync(ActivityExecutionContext context)
    {
        _recorder.CompensatedNodeIds.Add(context.Node.Id);
        return Task.CompletedTask;
    }
}
