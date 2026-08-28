// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.UserTasks;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Events;

/// <summary>
/// 人工任务事件契约测试
/// </summary>
/// <remarks>
/// 这三个事件是应用侧发通知的唯一数据来源，位置参数顺序即通知模板的取值顺序；
/// 办理事件里 ActorId 与 Outcome 相邻且都是字符串，顺序写反不会编译报错，只会静默把办理人当结果发出去，
/// 所以逐个位置断言而不是只断言属性。
/// </remarks>
public class WorkflowUserTaskEventsTests
{
    /// <summary>
    /// 任务创建事件携带任务本体与抄送人集合
    /// </summary>
    [Fact]
    public void CreatedEvent_ExposesTaskAndCcUserIds()
    {
        var task = new WorkflowUserTask
        {
            TaskId = "bm-1",
            InstanceId = "ins-1",
            NodeId = "approve",
            AssigneeId = "u-1",
            Title = "张三的请假"
        };
        var ccUserIds = new List<string> { "u-2", "u-3" };

        var data = new WorkflowUserTaskCreatedEventData(task, ccUserIds);
        var (eventTask, eventCcUserIds) = data;

        Assert.Same(task, eventTask);
        Assert.Same(ccUserIds, eventCcUserIds);
        Assert.Equal(2, data.CcUserIds.Count);
        Assert.True(typeof(WorkflowUserTaskCreatedEventData).IsSealed);
    }

    /// <summary>
    /// 任务办理事件按六个位置参数暴露且顺序锁定
    /// </summary>
    [Fact]
    public void CompletedEvent_ExposesSixPositionalMembersInOrder()
    {
        var data = new WorkflowUserTaskCompletedEventData(
            "bm-1",
            "ins-1",
            "approve",
            "u-1",
            WorkflowUserTaskOutcomes.Approved,
            "同意");

        var (taskId, instanceId, nodeId, actorId, outcome, comment) = data;

        Assert.Equal("bm-1", taskId);
        Assert.Equal("ins-1", instanceId);
        Assert.Equal("approve", nodeId);
        Assert.Equal("u-1", actorId);
        Assert.Equal("approved", outcome);
        Assert.Equal("同意", comment);
        Assert.Equal("bm-1", data.TaskId);
        Assert.Equal("u-1", data.ActorId);
        Assert.Equal("approved", data.Outcome);
    }

    /// <summary>
    /// 办理意见可为空
    /// </summary>
    [Fact]
    public void CompletedEvent_AllowsNullComment()
    {
        var data = new WorkflowUserTaskCompletedEventData("bm-1", "ins-1", "approve", "u-1", WorkflowUserTaskOutcomes.Rejected, null);

        Assert.Null(data.Comment);
        Assert.Equal("rejected", data.Outcome);
    }

    /// <summary>
    /// 办理结果不同的两条办理事件不相等
    /// </summary>
    [Fact]
    public void CompletedEvent_Equality_DistinguishesOutcome()
    {
        var approved = new WorkflowUserTaskCompletedEventData("bm-1", "ins-1", "approve", "u-1", WorkflowUserTaskOutcomes.Approved, null);
        var rejected = new WorkflowUserTaskCompletedEventData("bm-1", "ins-1", "approve", "u-1", WorkflowUserTaskOutcomes.Rejected, null);
        var sameAsApproved = new WorkflowUserTaskCompletedEventData("bm-1", "ins-1", "approve", "u-1", WorkflowUserTaskOutcomes.Approved, null);

        Assert.NotEqual(approved, rejected);
        Assert.Equal(approved, sameAsApproved);
        Assert.Equal(approved.GetHashCode(), sameAsApproved.GetHashCode());
    }

    /// <summary>
    /// 任务转办事件按五个位置参数区分操作人与新受理人
    /// </summary>
    [Fact]
    public void TransferredEvent_DistinguishesActorFromTargetAssignee()
    {
        var data = new WorkflowUserTaskTransferredEventData("bm-1", "ins-1", "u-1", "u-9", "出差代办");

        var (taskId, instanceId, actorId, targetAssigneeId, comment) = data;

        Assert.Equal("bm-1", taskId);
        Assert.Equal("ins-1", instanceId);
        Assert.Equal("u-1", actorId);
        Assert.Equal("u-9", targetAssigneeId);
        Assert.Equal("出差代办", comment);
        Assert.NotEqual(data.ActorId, data.TargetAssigneeId);
        Assert.True(typeof(WorkflowUserTaskTransferredEventData).IsSealed);
    }

    /// <summary>
    /// 转办意见可为空
    /// </summary>
    [Fact]
    public void TransferredEvent_AllowsNullComment()
    {
        var data = new WorkflowUserTaskTransferredEventData("bm-1", "ins-1", "u-1", "u-9", null);

        Assert.Null(data.Comment);
    }
}
