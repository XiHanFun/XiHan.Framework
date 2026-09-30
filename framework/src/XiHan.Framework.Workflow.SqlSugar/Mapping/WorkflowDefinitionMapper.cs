// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.SqlSugar.Entities;

namespace XiHan.Framework.Workflow.SqlSugar.Mapping;

/// <summary>
/// 流程定义契约与实体的双向映射
/// </summary>
public static class WorkflowDefinitionMapper
{
    /// <summary>
    /// 把流程定义转换为实体
    /// </summary>
    /// <param name="definition">流程定义</param>
    /// <returns>定义实体</returns>
    public static SysWorkflowDefinition ToEntity(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new SysWorkflowDefinition(definition.Id)
        {
            Code = definition.Code,
            Name = definition.Name,
            Version = definition.Version,
            Description = definition.Description,
            Category = definition.Category,
            Status = (int)definition.Status,
            EnableCompensation = definition.EnableCompensation,
            NodesJson = WorkflowJsonColumn.Serialize(definition.Nodes),
            TransitionsJson = WorkflowJsonColumn.Serialize(definition.Transitions),
            VariablesJson = WorkflowJsonColumn.Serialize(definition.Variables),
            ExtraProperties = WorkflowJsonColumn.Serialize(definition.ExtraProperties),
            OwnerTenantId = definition.TenantId,
            CreationTime = definition.CreationTime,
            PublishTime = definition.PublishTime
        };
    }

    /// <summary>
    /// 把实体转换为流程定义
    /// </summary>
    /// <param name="entity">定义实体</param>
    /// <returns>流程定义</returns>
    public static WorkflowDefinition ToDefinition(SysWorkflowDefinition entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new WorkflowDefinition
        {
            Id = entity.BasicId,
            Code = entity.Code,
            Name = entity.Name,
            Version = entity.Version,
            Description = entity.Description,
            Category = entity.Category,
            Status = (WorkflowDefinitionStatus)entity.Status,
            EnableCompensation = entity.EnableCompensation,
            Nodes = WorkflowJsonColumn.Deserialize<List<WorkflowNode>>(entity.NodesJson),
            Transitions = WorkflowJsonColumn.Deserialize<List<WorkflowTransition>>(entity.TransitionsJson),
            Variables = WorkflowJsonColumn.Deserialize<List<WorkflowVariableDefinition>>(entity.VariablesJson),
            ExtraProperties = WorkflowJsonColumn.Deserialize<Dictionary<string, string>>(entity.ExtraProperties),
            TenantId = entity.OwnerTenantId,
            CreationTime = entity.CreationTime,
            PublishTime = entity.PublishTime
        };
    }
}
