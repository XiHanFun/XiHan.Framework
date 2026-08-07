// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// <summary>
    /// 执行活动，尝试次数未达到指定次数时返回故障，达到后返回完成并写入 flakyDone 变量
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>执行结果（完成或故障）</returns>
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
    /// <summary>
    /// 执行活动，直接抛出取消异常
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>不会正常返回，始终抛出 <see cref="OperationCanceledException"/></returns>
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

    /// <summary>
    /// 执行活动，直接返回完成
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>执行结果（完成）</returns>
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }

    /// <summary>
    /// 补偿活动，把被补偿的节点标识追加到补偿记录器
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>任务</returns>
    public Task CompensateAsync(ActivityExecutionContext context)
    {
        _recorder.CompensatedNodeIds.Add(context.Node.Id);
        return Task.CompletedTask;
    }
}
