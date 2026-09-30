// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.Workflow.SqlSugar.Entities;

/// <summary>
/// 流程节点实例实体
/// </summary>
[SugarTable("sys_workflow_node_instance")]
[TableInitialization(Target = DbInitializationTarget.Platform)]
[SugarIndex("idx_{table}_instance", nameof(InstanceId), OrderByType.Asc, nameof(StartTime), OrderByType.Asc, nameof(Sequence), OrderByType.Asc)]
public class SysWorkflowNodeInstance : SugarEntity<string>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysWorkflowNodeInstance() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">节点实例标识</param>
    public SysWorkflowNodeInstance(string basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 所属流程实例标识
    /// </summary>
    [SugarColumn(ColumnName = "Instance_Id", Length = 255, IsNullable = false, ColumnDescription = "所属流程实例标识")]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 节点标识
    /// </summary>
    [SugarColumn(ColumnName = "Node_Id", Length = 255, IsNullable = false, ColumnDescription = "节点标识")]
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称快照
    /// </summary>
    [SugarColumn(ColumnName = "Name", Length = 256, IsNullable = false, ColumnDescription = "节点名称快照")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 活动类型编码
    /// </summary>
    [SugarColumn(ColumnName = "Activity_Type", Length = 128, IsNullable = false, ColumnDescription = "活动类型编码")]
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// 状态，1 执行中，2 已挂起，3 已完成，4 已取消，5 已故障，6 已补偿
    /// </summary>
    [SugarColumn(ColumnName = "Status", IsNullable = false, ColumnDescription = "状态，1 执行中，2 已挂起，3 已完成，4 已取消，5 已故障，6 已补偿")]
    public int Status { get; set; }

    /// <summary>
    /// 尝试次数
    /// </summary>
    [SugarColumn(ColumnName = "Try_Count", IsNullable = false, ColumnDescription = "尝试次数")]
    public int TryCount { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    [SugarColumn(ColumnName = "Start_Time", IsNullable = false, ColumnDescription = "开始时间")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    [SugarColumn(ColumnName = "End_Time", IsNullable = true, ColumnDescription = "结束时间")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 输入快照的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Inputs_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "输入快照的 JSON")]
    public string InputsJson { get; set; } = "{}";

    /// <summary>
    /// 输出快照的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Outputs_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "输出快照的 JSON")]
    public string OutputsJson { get; set; } = "{}";

    /// <summary>
    /// 活动私有状态的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "State_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "活动私有状态的 JSON")]
    public string StateJson { get; set; } = "{}";

    /// <summary>
    /// 故障信息
    /// </summary>
    [SugarColumn(ColumnName = "Fault_Message", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "故障信息")]
    public string? FaultMessage { get; set; }

    /// <summary>
    /// 补偿时间
    /// </summary>
    [SugarColumn(ColumnName = "Compensated_Time", IsNullable = true, ColumnDescription = "补偿时间")]
    public DateTime? CompensatedTime { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? OwnerTenantId { get; set; }

    /// <summary>
    /// 创建顺序，由节点实例标识解析得到，无法解析时为 0
    /// </summary>
    [SugarColumn(ColumnName = "Sequence", IsNullable = false, ColumnDescription = "创建顺序，由节点实例标识解析得到，无法解析时为 0")]
    public long Sequence { get; set; }
}
