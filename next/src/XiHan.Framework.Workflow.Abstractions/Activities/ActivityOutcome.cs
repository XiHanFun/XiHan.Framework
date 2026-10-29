// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动执行的结果。
/// </summary>
/// <remarks>
/// 完成（输出与结果标签）、挂起（书签）、结束或终止实例；可附带要启动的子流程。失败不在这里表达，返回失败的 <see cref="Result"/> 即故障。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ActivityOutcome
{
    /// <summary>
    /// 只经工厂方法创建。
    /// </summary>
    private ActivityOutcome(ActivityOutcomeKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// 种类。
    /// </summary>
    public ActivityOutcomeKind Kind { get; }

    /// <summary>
    /// 输出。
    /// </summary>
    /// <remarks>
    /// 完成时合并进实例变量。
    /// </remarks>
    public JsonObject? Outputs { get; private init; }

    /// <summary>
    /// 结果标签。
    /// </summary>
    /// <remarks>
    /// 写进节点输出 <c>outcome</c> 并作为出边条件里的 <c>outcome</c>。
    /// </remarks>
    public string? Label { get; private init; }

    /// <summary>
    /// 挂起时请求的书签。
    /// </summary>
    public IReadOnlyList<BookmarkRequest> Bookmarks { get; private init; } = [];

    /// <summary>
    /// 要启动的子流程（锁释放后启动）。
    /// </summary>
    public IReadOnlyList<WorkflowStartRequest> Children { get; private init; } = [];

    /// <summary>
    /// 终止原因。
    /// </summary>
    public string? Reason { get; private init; }

    /// <summary>
    /// 完成。
    /// </summary>
    public static ActivityOutcome Complete(JsonObject? outputs = null, string? label = null) => new(ActivityOutcomeKind.Completed) { Outputs = outputs, Label = label };

    /// <summary>
    /// 完成并结束实例。
    /// </summary>
    public static ActivityOutcome EndInstance(JsonObject? outputs = null, string? label = null) => new(ActivityOutcomeKind.EndInstance) { Outputs = outputs, Label = label };

    /// <summary>
    /// 终止实例。
    /// </summary>
    public static ActivityOutcome TerminateInstance(string? reason = null) => new(ActivityOutcomeKind.Terminated) { Reason = reason };

    /// <summary>
    /// 挂起。
    /// </summary>
    /// <remarks>
    /// 没有书签时保留节点现有的书签。
    /// </remarks>
    public static ActivityOutcome Suspend(params IReadOnlyList<BookmarkRequest> bookmarks) => new(ActivityOutcomeKind.Suspended) { Bookmarks = bookmarks ?? [] };

    /// <summary>
    /// 挂起并先把 <paramref name="outputs"/> 合并进实例变量（如会签途中记录各人的意见）。
    /// </summary>
    public static ActivityOutcome SuspendWithOutputs(JsonObject? outputs, params IReadOnlyList<BookmarkRequest> bookmarks) => new(ActivityOutcomeKind.Suspended) { Outputs = outputs, Bookmarks = bookmarks ?? [] };

    /// <summary>
    /// 完成并启动子流程（不等待）。
    /// </summary>
    public static ActivityOutcome CompleteWithChildren(IReadOnlyList<WorkflowStartRequest> children, JsonObject? outputs = null, string? label = null)
        => new(ActivityOutcomeKind.Completed) { Children = children ?? throw new ArgumentNullException(nameof(children)), Outputs = outputs, Label = label };

    /// <summary>
    /// 挂起并启动子流程（等待回调）。
    /// </summary>
    public static ActivityOutcome SuspendWithChildren(IReadOnlyList<WorkflowStartRequest> children, params IReadOnlyList<BookmarkRequest> bookmarks)
        => new(ActivityOutcomeKind.Suspended) { Children = children ?? throw new ArgumentNullException(nameof(children)), Bookmarks = bookmarks ?? [] };
}
