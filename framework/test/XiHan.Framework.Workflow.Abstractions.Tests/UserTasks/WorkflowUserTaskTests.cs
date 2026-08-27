// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.UserTasks;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 人工待办任务模型测试
/// </summary>
/// <remarks>
/// 待办是人工任务书签的业务视图，任务标识即书签标识——这条等价关系是"办理任务 = 恢复书签"的桥，
/// 一旦任务另起标识体系，办理入口就再也找不到对应书签了，因此显式验证。
/// </remarks>
public class WorkflowUserTaskTests
{
    /// <summary>
    /// 新建待办的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreEmpty()
    {
        var task = new WorkflowUserTask();

        Assert.Equal(string.Empty, task.TaskId);
        Assert.Equal(string.Empty, task.InstanceId);
        Assert.Equal(string.Empty, task.InstanceName);
        Assert.Equal(string.Empty, task.DefinitionCode);
        Assert.Equal(string.Empty, task.NodeId);
        Assert.Equal(string.Empty, task.NodeInstanceId);
        Assert.Equal(string.Empty, task.Title);
        Assert.Equal(string.Empty, task.AssigneeId);
        Assert.Null(task.CorrelationId);
        Assert.Empty(task.FormData);
        Assert.Equal(default(DateTime), task.CreationTime);
        Assert.Null(task.TenantId);
    }

    /// <summary>
    /// 待办任务标识与来源书签标识是同一个值
    /// </summary>
    [Fact]
    public void TaskId_EqualsSourceBookmarkId()
    {
        var bookmark = new WorkflowBookmark
        {
            Id = "bm-1",
            InstanceId = "ins-1",
            NodeId = "approve",
            NodeInstanceId = "ni-1",
            Kind = WorkflowBookmarkKinds.UserTask,
            Key = "u-1"
        };

        var task = new WorkflowUserTask
        {
            TaskId = bookmark.Id,
            InstanceId = bookmark.InstanceId,
            NodeId = bookmark.NodeId,
            NodeInstanceId = bookmark.NodeInstanceId,
            AssigneeId = bookmark.Key ?? string.Empty
        };

        Assert.Equal(bookmark.Id, task.TaskId);
        Assert.Equal(bookmark.NodeInstanceId, task.NodeInstanceId);
        Assert.Equal(bookmark.Key, task.AssigneeId);
    }

    /// <summary>
    /// 不同待办实例的表单数据互相独立
    /// </summary>
    [Fact]
    public void FormData_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowUserTask();
        var second = new WorkflowUserTask();

        first.FormData["days"] = 3;

        Assert.Empty(second.FormData);
        Assert.NotSame(first.FormData, second.FormData);
    }

    /// <summary>
    /// 待办 JSON 往返保留标量字段与表单数据
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesScalarFieldsAndFormData()
    {
        var task = new WorkflowUserTask
        {
            TaskId = "bm-1",
            InstanceId = "ins-1",
            InstanceName = "张三的请假",
            DefinitionCode = "leave",
            NodeId = "approve",
            NodeInstanceId = "ni-1",
            Title = "请假审批",
            AssigneeId = "u-1",
            CorrelationId = "biz-1",
            CreationTime = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc),
            TenantId = 5L,
            FormData = { ["days"] = 3, ["reason"] = "年假" }
        };

        var restored = JsonSerializer.Deserialize<WorkflowUserTask>(JsonSerializer.Serialize(task));

        Assert.NotNull(restored);
        Assert.Equal("bm-1", restored.TaskId);
        Assert.Equal("ins-1", restored.InstanceId);
        Assert.Equal("张三的请假", restored.InstanceName);
        Assert.Equal("leave", restored.DefinitionCode);
        Assert.Equal("approve", restored.NodeId);
        Assert.Equal("ni-1", restored.NodeInstanceId);
        Assert.Equal("请假审批", restored.Title);
        Assert.Equal("u-1", restored.AssigneeId);
        Assert.Equal("biz-1", restored.CorrelationId);
        Assert.Equal(task.CreationTime, restored.CreationTime);
        Assert.Equal(5L, restored.TenantId);
        Assert.Equal(3m, WorkflowValueConverter.Normalize(restored.FormData["days"]));
        Assert.Equal("年假", WorkflowValueConverter.Normalize(restored.FormData["reason"]));
    }
}
