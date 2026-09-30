// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Entities;

namespace XiHan.Framework.Workflow.SqlSugar.Mapping;

/// <summary>
/// 流程书签契约与实体的双向映射
/// </summary>
public static class WorkflowBookmarkMapper
{
    /// <summary>
    /// 把书签转换为实体
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <returns>书签实体</returns>
    public static SysWorkflowBookmark ToEntity(WorkflowBookmark bookmark)
    {
        ArgumentNullException.ThrowIfNull(bookmark);

        return new SysWorkflowBookmark(bookmark.Id)
        {
            InstanceId = bookmark.InstanceId,
            NodeId = bookmark.NodeId,
            NodeInstanceId = bookmark.NodeInstanceId,
            Kind = bookmark.Kind,
            BookmarkKey = bookmark.Key,
            PayloadJson = WorkflowJsonColumn.Serialize(bookmark.Payload),
            DueTime = bookmark.DueTime,
            CorrelationId = bookmark.CorrelationId,
            CreationTime = bookmark.CreationTime,
            OwnerTenantId = bookmark.TenantId
        };
    }

    /// <summary>
    /// 把实体转换为书签
    /// </summary>
    /// <param name="entity">书签实体</param>
    /// <returns>书签</returns>
    public static WorkflowBookmark ToBookmark(SysWorkflowBookmark entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new WorkflowBookmark
        {
            Id = entity.BasicId,
            InstanceId = entity.InstanceId,
            NodeId = entity.NodeId,
            NodeInstanceId = entity.NodeInstanceId,
            Kind = entity.Kind,
            Key = entity.BookmarkKey,
            Payload = WorkflowJsonColumn.Deserialize<Dictionary<string, object?>>(entity.PayloadJson),
            DueTime = entity.DueTime,
            CorrelationId = entity.CorrelationId,
            CreationTime = entity.CreationTime,
            TenantId = entity.OwnerTenantId
        };
    }
}
