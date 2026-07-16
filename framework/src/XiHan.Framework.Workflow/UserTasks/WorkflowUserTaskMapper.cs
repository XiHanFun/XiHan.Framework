#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowUserTaskMapper
// Guid:a04c68f1-97be-4d25-8e13-52c70d9b6ea8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:58:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.UserTasks;

namespace XiHan.Framework.Workflow.UserTasks;

/// <summary>
/// 人工任务视图映射器（书签到待办任务的转换）
/// </summary>
public static class WorkflowUserTaskMapper
{
    /// <summary>
    /// 把人工任务书签转换为待办任务视图
    /// </summary>
    /// <param name="bookmark">人工任务书签</param>
    /// <param name="instance">所属流程实例</param>
    /// <returns>待办任务视图</returns>
    public static WorkflowUserTask ToUserTask(WorkflowBookmark bookmark, WorkflowInstance instance)
    {
        var formData = bookmark.Payload.TryGetValue(WorkflowUserTaskPayloadKeys.FormData, out var rawFormData)
            ? WorkflowValueConverter.ConvertTo<Dictionary<string, object?>>(rawFormData) ?? []
            : [];

        return new WorkflowUserTask
        {
            TaskId = bookmark.Id,
            InstanceId = instance.Id,
            InstanceName = instance.Name,
            DefinitionCode = instance.DefinitionCode,
            NodeId = bookmark.NodeId,
            NodeInstanceId = bookmark.NodeInstanceId,
            Title = bookmark.Payload.TryGetValue(WorkflowUserTaskPayloadKeys.Title, out var title)
                ? WorkflowValueConverter.ConvertTo<string>(title) ?? string.Empty
                : string.Empty,
            AssigneeId = bookmark.Key ?? string.Empty,
            CorrelationId = bookmark.CorrelationId ?? instance.CorrelationId,
            FormData = formData,
            CreationTime = bookmark.CreationTime,
            TenantId = bookmark.TenantId
        };
    }
}
