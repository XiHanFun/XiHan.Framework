// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Entities;

namespace XiHan.Framework.Workflow.SqlSugar.Mapping;

/// <summary>
/// 流程实例契约与实体的双向映射
/// </summary>
public static class WorkflowInstanceMapper
{
    /// <summary>
    /// 把流程实例转换为实体
    /// </summary>
    /// <param name="instance">流程实例</param>
    /// <returns>实例实体</returns>
    public static SysWorkflowInstance ToEntity(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return new SysWorkflowInstance(instance.Id)
        {
            DefinitionId = instance.DefinitionId,
            DefinitionCode = instance.DefinitionCode,
            DefinitionVersion = instance.DefinitionVersion,
            Name = instance.Name,
            Status = (int)instance.Status,
            VariablesJson = WorkflowJsonColumn.Serialize(instance.Variables),
            JoinStatesJson = WorkflowJsonColumn.Serialize(instance.JoinStates),
            CorrelationId = instance.CorrelationId,
            StarterId = instance.StarterId,
            ParentInstanceId = instance.ParentInstanceId,
            ParentNodeInstanceId = instance.ParentNodeInstanceId,
            Depth = instance.Depth,
            OwnerTenantId = instance.TenantId,
            CreationTime = instance.CreationTime,
            StartTime = instance.StartTime,
            EndTime = instance.EndTime,
            FaultMessage = instance.FaultMessage,
            FaultNodeId = instance.FaultNodeId,
            FaultNodeInstanceId = instance.FaultNodeInstanceId,
            CancellationReason = instance.CancellationReason
        };
    }

    /// <summary>
    /// 把实体转换为流程实例
    /// </summary>
    /// <param name="entity">实例实体</param>
    /// <returns>流程实例</returns>
    public static WorkflowInstance ToInstance(SysWorkflowInstance entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new WorkflowInstance
        {
            Id = entity.BasicId,
            DefinitionId = entity.DefinitionId,
            DefinitionCode = entity.DefinitionCode,
            DefinitionVersion = entity.DefinitionVersion,
            Name = entity.Name,
            Status = (WorkflowInstanceStatus)entity.Status,
            Variables = WorkflowJsonColumn.Deserialize<Dictionary<string, object?>>(entity.VariablesJson),
            JoinStates = WorkflowJsonColumn.Deserialize<Dictionary<string, WorkflowJoinState>>(entity.JoinStatesJson),
            CorrelationId = entity.CorrelationId,
            StarterId = entity.StarterId,
            ParentInstanceId = entity.ParentInstanceId,
            ParentNodeInstanceId = entity.ParentNodeInstanceId,
            Depth = entity.Depth,
            TenantId = entity.OwnerTenantId,
            CreationTime = entity.CreationTime,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            FaultMessage = entity.FaultMessage,
            FaultNodeId = entity.FaultNodeId,
            FaultNodeInstanceId = entity.FaultNodeInstanceId,
            CancellationReason = entity.CancellationReason
        };
    }
}
