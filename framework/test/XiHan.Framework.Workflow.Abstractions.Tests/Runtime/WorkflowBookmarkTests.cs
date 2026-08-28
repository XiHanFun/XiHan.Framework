// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Runtime;

/// <summary>
/// 流程书签模型测试
/// </summary>
/// <remarks>
/// 书签是实例挂起后唯一的可恢复入口，Kind 决定 Key 与 DueTime 的语义；
/// 三个可空字段（Key/DueTime/CorrelationId）的"为空即不限"语义必须锁死：
/// 例如信号书签 CorrelationId 为空表示接受广播，写成空串就再也匹配不上了。
/// </remarks>
public class WorkflowBookmarkTests
{
    /// <summary>
    /// 新建书签的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreEmptyAndUnbounded()
    {
        var bookmark = new WorkflowBookmark();

        Assert.Equal(string.Empty, bookmark.Id);
        Assert.Equal(string.Empty, bookmark.InstanceId);
        Assert.Equal(string.Empty, bookmark.NodeId);
        Assert.Equal(string.Empty, bookmark.NodeInstanceId);
        Assert.Equal(string.Empty, bookmark.Kind);
        Assert.Null(bookmark.Key);
        Assert.Empty(bookmark.Payload);
        Assert.Null(bookmark.DueTime);
        Assert.Null(bookmark.CorrelationId);
        Assert.Equal(default(DateTime), bookmark.CreationTime);
        Assert.Null(bookmark.TenantId);
    }

    /// <summary>
    /// 不同书签实例的载荷字典互相独立
    /// </summary>
    [Fact]
    public void Payload_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowBookmark();
        var second = new WorkflowBookmark();

        first.Payload["title"] = "张三的请假";

        Assert.Empty(second.Payload);
        Assert.NotSame(first.Payload, second.Payload);
    }

    /// <summary>
    /// 人工任务书签以受理人作为索引键
    /// </summary>
    [Fact]
    public void UserTaskBookmark_UsesAssigneeAsKeyWithoutDueTime()
    {
        var bookmark = new WorkflowBookmark
        {
            Id = "bm-1",
            Kind = WorkflowBookmarkKinds.UserTask,
            Key = "u-1",
            Payload = { ["title"] = "张三的请假" }
        };

        Assert.Equal(WorkflowBookmarkKinds.UserTask, bookmark.Kind);
        Assert.Equal("u-1", bookmark.Key);
        Assert.Null(bookmark.DueTime);
        Assert.Equal("张三的请假", bookmark.Payload["title"]);
    }

    /// <summary>
    /// 定时类书签以到期时间驱动恢复
    /// </summary>
    [Theory]
    [InlineData(WorkflowBookmarkKinds.Timer)]
    [InlineData(WorkflowBookmarkKinds.Retry)]
    [InlineData(WorkflowBookmarkKinds.NodeTimeout)]
    public void TimerLikeBookmark_CarriesDueTime(string kind)
    {
        var due = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);

        var bookmark = new WorkflowBookmark { Kind = kind, DueTime = due };

        Assert.Equal(kind, bookmark.Kind);
        Assert.Equal(due, bookmark.DueTime);
    }

    /// <summary>
    /// 书签 JSON 往返保留种类、索引键与载荷
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesKindKeyAndPayload()
    {
        var bookmark = new WorkflowBookmark
        {
            Id = "bm-1",
            InstanceId = "ins-1",
            NodeId = "wait",
            NodeInstanceId = "ni-1",
            Kind = WorkflowBookmarkKinds.Signal,
            Key = "paid",
            CorrelationId = "biz-1",
            DueTime = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc),
            CreationTime = new DateTime(2024, 5, 6, 0, 0, 0, DateTimeKind.Utc),
            TenantId = 3L,
            Payload = { ["title"] = "等待支付", ["retry"] = 2 }
        };

        var restored = JsonSerializer.Deserialize<WorkflowBookmark>(JsonSerializer.Serialize(bookmark));

        Assert.NotNull(restored);
        Assert.Equal("bm-1", restored.Id);
        Assert.Equal("ins-1", restored.InstanceId);
        Assert.Equal("wait", restored.NodeId);
        Assert.Equal("ni-1", restored.NodeInstanceId);
        Assert.Equal(WorkflowBookmarkKinds.Signal, restored.Kind);
        Assert.Equal("paid", restored.Key);
        Assert.Equal("biz-1", restored.CorrelationId);
        Assert.Equal(bookmark.DueTime, restored.DueTime);
        Assert.Equal(bookmark.CreationTime, restored.CreationTime);
        Assert.Equal(3L, restored.TenantId);
        Assert.Equal("等待支付", WorkflowValueConverter.Normalize(restored.Payload["title"]));
        Assert.Equal(2m, WorkflowValueConverter.Normalize(restored.Payload["retry"]));
    }

    /// <summary>
    /// 广播信号书签往返后相关性标识仍为空
    /// </summary>
    [Fact]
    public void JsonRoundTrip_BroadcastSignalBookmark_KeepsNullCorrelationId()
    {
        var restored = JsonSerializer.Deserialize<WorkflowBookmark>(JsonSerializer.Serialize(
            new WorkflowBookmark { Kind = WorkflowBookmarkKinds.Signal, Key = "paid" }));

        Assert.NotNull(restored);
        Assert.Null(restored.CorrelationId);
        Assert.Null(restored.DueTime);
        Assert.Empty(restored.Payload);
    }
}
