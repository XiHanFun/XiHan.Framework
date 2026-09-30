// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Mapping;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程书签映射测试
/// </summary>
public class WorkflowBookmarkMapperTests
{
    /// <summary>
    /// 书签往返一致
    /// </summary>
    [Fact]
    public void 书签往返一致()
    {
        var bookmark = new WorkflowBookmark
        {
            Id = "4001",
            InstanceId = "2001",
            NodeId = "approve",
            NodeInstanceId = "3001",
            Kind = WorkflowBookmarkKinds.UserTask,
            Key = "u1",
            Payload = new Dictionary<string, object?> { ["title"] = "单据 B001 审批" },
            DueTime = new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc),
            CorrelationId = "ORDER-1",
            CreationTime = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc),
            TenantId = 9
        };

        var entity = WorkflowBookmarkMapper.ToEntity(bookmark);
        var restored = WorkflowBookmarkMapper.ToBookmark(entity);

        Assert.Equal("u1", entity.BookmarkKey);
        Assert.Equal(9, entity.OwnerTenantId);
        Assert.Equal(bookmark.Id, restored.Id);
        Assert.Equal(bookmark.InstanceId, restored.InstanceId);
        Assert.Equal(bookmark.NodeId, restored.NodeId);
        Assert.Equal(bookmark.NodeInstanceId, restored.NodeInstanceId);
        Assert.Equal(bookmark.Kind, restored.Kind);
        Assert.Equal(bookmark.Key, restored.Key);
        Assert.Equal(bookmark.DueTime, restored.DueTime);
        Assert.Equal(bookmark.CorrelationId, restored.CorrelationId);
        Assert.Equal(bookmark.CreationTime, restored.CreationTime);
        Assert.Equal(bookmark.TenantId, restored.TenantId);
        Assert.Equal("单据 B001 审批", WorkflowValueConverter.ConvertTo<string>(restored.Payload["title"]));
    }

    /// <summary>
    /// 相关性的空串与空值各自保留
    /// </summary>
    [Fact]
    public void 相关性的空串与空值各自保留()
    {
        var empty = new WorkflowBookmark { Id = "1", Kind = WorkflowBookmarkKinds.Signal, CorrelationId = string.Empty };
        var none = new WorkflowBookmark { Id = "2", Kind = WorkflowBookmarkKinds.Signal, CorrelationId = null };

        Assert.Equal(string.Empty, WorkflowBookmarkMapper.ToBookmark(WorkflowBookmarkMapper.ToEntity(empty)).CorrelationId);
        Assert.Null(WorkflowBookmarkMapper.ToBookmark(WorkflowBookmarkMapper.ToEntity(none)).CorrelationId);
    }
}
