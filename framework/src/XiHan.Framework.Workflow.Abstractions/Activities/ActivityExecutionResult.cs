// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动执行结果
/// </summary>
public sealed class ActivityExecutionResult
{
    private ActivityExecutionResult()
    {
    }

    /// <summary>
    /// 结果种类
    /// </summary>
    public ActivityExecutionResultKind Kind { get; private init; }

    /// <summary>
    /// 输出（完成时合并进实例变量）
    /// </summary>
    public Dictionary<string, object?> Outputs { get; private init; } = [];

    /// <summary>
    /// 活动结果值（作为 outcome 变量参与出边条件求值）
    /// </summary>
    public string? Outcome { get; private init; }

    /// <summary>
    /// 书签请求（挂起时声明的等待点；恢复返回挂起且请求为空时保留既有兄弟书签）
    /// </summary>
    public List<WorkflowBookmarkRequest> Bookmarks { get; private init; } = [];

    /// <summary>
    /// 子流程启动请求（引擎在当前执行批次结束并释放实例锁后启动，避免父子实例锁重入）
    /// </summary>
    public List<WorkflowStartRequest> ChildStartRequests { get; private init; } = [];

    /// <summary>
    /// 故障信息
    /// </summary>
    public string? FaultMessage { get; private init; }

    /// <summary>
    /// 创建完成结果
    /// </summary>
    /// <param name="outputs">输出</param>
    /// <param name="outcome">活动结果值</param>
    /// <returns>完成结果</returns>
    public static ActivityExecutionResult Complete(Dictionary<string, object?>? outputs = null, string? outcome = null)
    {
        return new ActivityExecutionResult
        {
            Kind = ActivityExecutionResultKind.Completed,
            Outputs = outputs ?? [],
            Outcome = outcome
        };
    }

    /// <summary>
    /// 创建挂起结果
    /// </summary>
    /// <param name="bookmarks">书签请求</param>
    /// <returns>挂起结果</returns>
    public static ActivityExecutionResult Suspend(params WorkflowBookmarkRequest[] bookmarks)
    {
        return new ActivityExecutionResult
        {
            Kind = ActivityExecutionResultKind.Suspended,
            Bookmarks = [.. bookmarks]
        };
    }

    /// <summary>
    /// 创建完成结果（附带子流程启动请求，发后不理场景）
    /// </summary>
    /// <param name="childStartRequests">子流程启动请求</param>
    /// <param name="outputs">输出</param>
    /// <param name="outcome">活动结果值</param>
    /// <returns>完成结果</returns>
    public static ActivityExecutionResult CompleteWithChildren(
        IEnumerable<WorkflowStartRequest> childStartRequests,
        Dictionary<string, object?>? outputs = null,
        string? outcome = null)
    {
        return new ActivityExecutionResult
        {
            Kind = ActivityExecutionResultKind.Completed,
            Outputs = outputs ?? [],
            Outcome = outcome,
            ChildStartRequests = [.. childStartRequests]
        };
    }

    /// <summary>
    /// 创建挂起结果（附带子流程启动请求）
    /// </summary>
    /// <param name="childStartRequests">子流程启动请求</param>
    /// <param name="bookmarks">书签请求</param>
    /// <returns>挂起结果</returns>
    public static ActivityExecutionResult SuspendWithChildren(
        IEnumerable<WorkflowStartRequest> childStartRequests,
        params WorkflowBookmarkRequest[] bookmarks)
    {
        return new ActivityExecutionResult
        {
            Kind = ActivityExecutionResultKind.Suspended,
            Bookmarks = [.. bookmarks],
            ChildStartRequests = [.. childStartRequests]
        };
    }

    /// <summary>
    /// 创建故障结果
    /// </summary>
    /// <param name="message">故障信息</param>
    /// <returns>故障结果</returns>
    public static ActivityExecutionResult Fault(string message)
    {
        return new ActivityExecutionResult
        {
            Kind = ActivityExecutionResultKind.Faulted,
            FaultMessage = message
        };
    }
}
