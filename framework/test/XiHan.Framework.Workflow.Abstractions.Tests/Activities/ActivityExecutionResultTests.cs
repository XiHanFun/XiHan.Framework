// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Activities;

/// <summary>
/// 活动执行结果测试
/// </summary>
/// <remarks>
/// 该类型是活动与引擎之间唯一的返回契约：只能经静态工厂构造，
/// 且未显式赋值的集合必须是空集合而非 null——引擎会无条件遍历它们。
/// </remarks>
public class ActivityExecutionResultTests
{
    /// <summary>
    /// 无参完成结果的种类与各集合默认值
    /// </summary>
    [Fact]
    public void Complete_WithoutArguments_ReturnsCompletedWithEmptyCollections()
    {
        var result = ActivityExecutionResult.Complete();

        Assert.Equal(ActivityExecutionResultKind.Completed, result.Kind);
        Assert.Empty(result.Outputs);
        Assert.Null(result.Outcome);
        Assert.Empty(result.Bookmarks);
        Assert.Empty(result.ChildStartRequests);
        Assert.Null(result.FaultMessage);
    }

    /// <summary>
    /// 完成结果保留传入的输出与活动结果值
    /// </summary>
    [Fact]
    public void Complete_WithOutputsAndOutcome_KeepsBoth()
    {
        var outputs = new Dictionary<string, object?>
        {
            ["comment"] = "同意",
            ["amount"] = 100
        };

        var result = ActivityExecutionResult.Complete(outputs, WorkflowUserTaskOutcomes.Approved);

        Assert.Equal(ActivityExecutionResultKind.Completed, result.Kind);
        Assert.Equal(WorkflowUserTaskOutcomes.Approved, result.Outcome);
        Assert.Equal(2, result.Outputs.Count);
        Assert.Equal("同意", result.Outputs["comment"]);
        Assert.Equal(100, result.Outputs["amount"]);
    }

    /// <summary>
    /// 显式传入 null 输出时退化为空字典
    /// </summary>
    [Fact]
    public void Complete_WithNullOutputs_FallsBackToEmptyDictionary()
    {
        var result = ActivityExecutionResult.Complete(null, "done");

        Assert.NotNull(result.Outputs);
        Assert.Empty(result.Outputs);
        Assert.Equal("done", result.Outcome);
    }

    /// <summary>
    /// 无书签的挂起结果仍是挂起种类
    /// </summary>
    [Fact]
    public void Suspend_WithoutBookmarks_ReturnsSuspendedWithEmptyBookmarks()
    {
        var result = ActivityExecutionResult.Suspend();

        Assert.Equal(ActivityExecutionResultKind.Suspended, result.Kind);
        Assert.Empty(result.Bookmarks);
        Assert.Empty(result.Outputs);
        Assert.Null(result.Outcome);
    }

    /// <summary>
    /// 挂起结果按传入顺序保留书签请求
    /// </summary>
    [Fact]
    public void Suspend_WithMultipleBookmarks_KeepsOrder()
    {
        var first = new WorkflowBookmarkRequest { Kind = WorkflowBookmarkKinds.UserTask, Key = "u-1" };
        var second = new WorkflowBookmarkRequest { Kind = WorkflowBookmarkKinds.Signal, Key = "paid" };

        var result = ActivityExecutionResult.Suspend(first, second);

        Assert.Equal(ActivityExecutionResultKind.Suspended, result.Kind);
        Assert.Equal(2, result.Bookmarks.Count);
        Assert.Same(first, result.Bookmarks[0]);
        Assert.Same(second, result.Bookmarks[1]);
    }

    /// <summary>
    /// 带子流程请求的完成结果同时保留输出、结果值与子请求
    /// </summary>
    [Fact]
    public void CompleteWithChildren_WithRequests_KeepsOutputsAndChildren()
    {
        var child = new WorkflowStartRequest { DefinitionCode = "sub", CorrelationId = "biz-1" };
        var outputs = new Dictionary<string, object?> { ["count"] = 1 };

        var result = ActivityExecutionResult.CompleteWithChildren([child], outputs, "spawned");

        Assert.Equal(ActivityExecutionResultKind.Completed, result.Kind);
        Assert.Equal("spawned", result.Outcome);
        Assert.Equal(1, result.Outputs["count"]);
        Assert.Single(result.ChildStartRequests);
        Assert.Same(child, result.ChildStartRequests[0]);
        Assert.Empty(result.Bookmarks);
    }

    /// <summary>
    /// 带子流程请求的挂起结果同时保留书签与子请求
    /// </summary>
    [Fact]
    public void SuspendWithChildren_WithRequests_KeepsBookmarksAndChildren()
    {
        var child = new WorkflowStartRequest { DefinitionId = "def-2" };
        var bookmark = new WorkflowBookmarkRequest { Kind = WorkflowBookmarkKinds.SubWorkflow, Key = "ni-1" };

        var result = ActivityExecutionResult.SuspendWithChildren([child], bookmark);

        Assert.Equal(ActivityExecutionResultKind.Suspended, result.Kind);
        Assert.Single(result.Bookmarks);
        Assert.Same(bookmark, result.Bookmarks[0]);
        Assert.Single(result.ChildStartRequests);
        Assert.Same(child, result.ChildStartRequests[0]);
        Assert.Empty(result.Outputs);
    }

    /// <summary>
    /// 故障结果携带故障信息且不产生输出与书签
    /// </summary>
    [Fact]
    public void Fault_WithMessage_ReturnsFaultedWithMessage()
    {
        var result = ActivityExecutionResult.Fault("远端返回 500");

        Assert.Equal(ActivityExecutionResultKind.Faulted, result.Kind);
        Assert.Equal("远端返回 500", result.FaultMessage);
        Assert.Empty(result.Outputs);
        Assert.Empty(result.Bookmarks);
        Assert.Empty(result.ChildStartRequests);
        Assert.Null(result.Outcome);
    }

    /// <summary>
    /// 类型封闭且只能经静态工厂构造
    /// </summary>
    /// <remarks>
    /// 构造器私有是刻意设计：结果种类与其携带数据必须成套出现，
    /// 一旦放开公共构造器就可能出现"故障却没有故障信息"的非法组合。
    /// </remarks>
    [Fact]
    public void Type_IsSealedAndHasNoPublicConstructor()
    {
        Assert.True(typeof(ActivityExecutionResult).IsSealed);
        Assert.Empty(typeof(ActivityExecutionResult).GetConstructors());
    }
}
