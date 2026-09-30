// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.Workflow.SqlSugar.Entities;

/// <summary>
/// 流程实例实体
/// </summary>
[SugarTable("sys_workflow_instance")]
[TableInitialization(Target = DbInitializationTarget.Platform)]
[SugarIndex("idx_{table}_status", nameof(Status), OrderByType.Asc, nameof(CreationTime), OrderByType.Desc)]
[SugarIndex("idx_{table}_definition_code", nameof(DefinitionCode), OrderByType.Asc, nameof(CreationTime), OrderByType.Desc)]
[SugarIndex("idx_{table}_correlation", nameof(CorrelationId), OrderByType.Asc)]
[SugarIndex("idx_{table}_parent", nameof(ParentInstanceId), OrderByType.Asc, nameof(CreationTime), OrderByType.Asc)]
public class SysWorkflowInstance : SugarEntity<string>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysWorkflowInstance() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">实例标识</param>
    public SysWorkflowInstance(string basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 定义标识
    /// </summary>
    [SugarColumn(ColumnName = "Definition_Id", Length = 255, IsNullable = false, ColumnDescription = "定义标识")]
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// 定义编码
    /// </summary>
    [SugarColumn(ColumnName = "Definition_Code", Length = 128, IsNullable = false, ColumnDescription = "定义编码")]
    public string DefinitionCode { get; set; } = string.Empty;

    /// <summary>
    /// 定义版本
    /// </summary>
    [SugarColumn(ColumnName = "Definition_Version", IsNullable = false, ColumnDescription = "定义版本")]
    public int DefinitionVersion { get; set; }

    /// <summary>
    /// 实例名称
    /// </summary>
    [SugarColumn(ColumnName = "Name", Length = 256, IsNullable = false, ColumnDescription = "实例名称")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 状态，1 运行中，2 已挂起，3 已完成，4 已取消，5 已故障，6 已终止
    /// </summary>
    [SugarColumn(ColumnName = "Status", IsNullable = false, ColumnDescription = "状态，1 运行中，2 已挂起，3 已完成，4 已取消，5 已故障，6 已终止")]
    public int Status { get; set; }

    /// <summary>
    /// 实例变量的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Variables_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "实例变量的 JSON")]
    public string VariablesJson { get; set; } = "{}";

    /// <summary>
    /// 汇聚网关波次状态的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Join_States_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "汇聚网关波次状态的 JSON")]
    public string JoinStatesJson { get; set; } = "{}";

    /// <summary>
    /// 业务相关性标识
    /// </summary>
    [SugarColumn(ColumnName = "Correlation_Id", Length = 255, IsNullable = true, ColumnDescription = "业务相关性标识")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 发起人标识
    /// </summary>
    [SugarColumn(ColumnName = "Starter_Id", Length = 255, IsNullable = true, ColumnDescription = "发起人标识")]
    public string? StarterId { get; set; }

    /// <summary>
    /// 父实例标识
    /// </summary>
    [SugarColumn(ColumnName = "Parent_Instance_Id", Length = 255, IsNullable = true, ColumnDescription = "父实例标识")]
    public string? ParentInstanceId { get; set; }

    /// <summary>
    /// 父节点实例标识
    /// </summary>
    [SugarColumn(ColumnName = "Parent_Node_Instance_Id", Length = 255, IsNullable = true, ColumnDescription = "父节点实例标识")]
    public string? ParentNodeInstanceId { get; set; }

    /// <summary>
    /// 实例深度
    /// </summary>
    [SugarColumn(ColumnName = "Depth", IsNullable = false, ColumnDescription = "实例深度")]
    public int Depth { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? OwnerTenantId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnName = "Creation_Time", IsNullable = false, ColumnDescription = "创建时间")]
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    [SugarColumn(ColumnName = "Start_Time", IsNullable = true, ColumnDescription = "开始时间")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    [SugarColumn(ColumnName = "End_Time", IsNullable = true, ColumnDescription = "结束时间")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 故障信息
    /// </summary>
    [SugarColumn(ColumnName = "Fault_Message", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "故障信息")]
    public string? FaultMessage { get; set; }

    /// <summary>
    /// 故障节点标识
    /// </summary>
    [SugarColumn(ColumnName = "Fault_Node_Id", Length = 255, IsNullable = true, ColumnDescription = "故障节点标识")]
    public string? FaultNodeId { get; set; }

    /// <summary>
    /// 故障节点实例标识
    /// </summary>
    [SugarColumn(ColumnName = "Fault_Node_Instance_Id", Length = 255, IsNullable = true, ColumnDescription = "故障节点实例标识")]
    public string? FaultNodeInstanceId { get; set; }

    /// <summary>
    /// 取消或终止原因
    /// </summary>
    [SugarColumn(ColumnName = "Cancellation_Reason", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "取消或终止原因")]
    public string? CancellationReason { get; set; }
}
