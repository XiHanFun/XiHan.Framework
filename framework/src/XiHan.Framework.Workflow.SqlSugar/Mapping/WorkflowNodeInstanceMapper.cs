// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Entities;

namespace XiHan.Framework.Workflow.SqlSugar.Mapping;

/// <summary>
/// 节点实例契约与实体的双向映射
/// </summary>
public static class WorkflowNodeInstanceMapper
{
    /// <summary>
    /// 把节点实例转换为实体
    /// </summary>
    /// <param name="nodeInstance">节点实例</param>
    /// <returns>节点实例实体</returns>
    public static SysWorkflowNodeInstance ToEntity(WorkflowNodeInstance nodeInstance)
    {
        ArgumentNullException.ThrowIfNull(nodeInstance);

        return new SysWorkflowNodeInstance(nodeInstance.Id)
        {
            InstanceId = nodeInstance.InstanceId,
            NodeId = nodeInstance.NodeId,
            Name = nodeInstance.Name,
            ActivityType = nodeInstance.ActivityType,
            Status = (int)nodeInstance.Status,
            TryCount = nodeInstance.TryCount,
            StartTime = nodeInstance.StartTime,
            EndTime = nodeInstance.EndTime,
            InputsJson = WorkflowJsonColumn.Serialize(nodeInstance.Inputs),
            OutputsJson = WorkflowJsonColumn.Serialize(nodeInstance.Outputs),
            StateJson = WorkflowJsonColumn.Serialize(nodeInstance.State),
            FaultMessage = nodeInstance.FaultMessage,
            CompensatedTime = nodeInstance.CompensatedTime,
            OwnerTenantId = nodeInstance.TenantId,
            Sequence = ParseSequence(nodeInstance.Id)
        };
    }

    /// <summary>
    /// 把实体转换为节点实例
    /// </summary>
    /// <param name="entity">节点实例实体</param>
    /// <returns>节点实例</returns>
    public static WorkflowNodeInstance ToNodeInstance(SysWorkflowNodeInstance entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new WorkflowNodeInstance
        {
            Id = entity.BasicId,
            InstanceId = entity.InstanceId,
            NodeId = entity.NodeId,
            Name = entity.Name,
            ActivityType = entity.ActivityType,
            Status = (WorkflowNodeInstanceStatus)entity.Status,
            TryCount = entity.TryCount,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Inputs = WorkflowJsonColumn.Deserialize<Dictionary<string, object?>>(entity.InputsJson),
            Outputs = WorkflowJsonColumn.Deserialize<Dictionary<string, object?>>(entity.OutputsJson),
            State = WorkflowJsonColumn.Deserialize<Dictionary<string, object?>>(entity.StateJson),
            FaultMessage = entity.FaultMessage,
            CompensatedTime = entity.CompensatedTime,
            TenantId = entity.OwnerTenantId
        };
    }

    /// <summary>
    /// 把节点实例标识解析为创建顺序，无法解析时返回 0
    /// </summary>
    /// <param name="id">节点实例标识</param>
    /// <returns>创建顺序</returns>
    public static long ParseSequence(string id)
    {
        return long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence) ? sequence : 0;
    }
}
