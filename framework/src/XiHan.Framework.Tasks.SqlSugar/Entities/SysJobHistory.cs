// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Entities;

/// <summary>
/// 定时任务执行历史实体，时间均为协调世界时
/// </summary>
[SugarTable("sys_job_history")]
[SugarIndex("idx_sys_job_history_name_started",
    nameof(SysJobHistory.JobName), OrderByType.Asc,
    nameof(SysJobHistory.StartedAt), OrderByType.Desc)]
[SugarIndex("idx_sys_job_history_started_at", nameof(SysJobHistory.StartedAt), OrderByType.Asc)]
public class SysJobHistory : SugarEntity<string>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysJobHistory() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">历史记录唯一标识</param>
    public SysJobHistory(string basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 实例唯一标识
    /// </summary>
    [SugarColumn(ColumnName = "Instance_Id", Length = 128, IsNullable = false, ColumnDescription = "实例唯一标识")]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 任务名称
    /// </summary>
    [SugarColumn(ColumnName = "Job_Name", Length = 256, IsNullable = false, ColumnDescription = "任务名称")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态
    /// </summary>
    [SugarColumn(ColumnName = "Status", IsNullable = false, ColumnDescription = "执行状态")]
    public int Status { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    [SugarColumn(ColumnName = "Started_At", IsNullable = false, ColumnDescription = "开始时间")]
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    [SugarColumn(ColumnName = "Completed_At", IsNullable = true, ColumnDescription = "完成时间")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    [SugarColumn(ColumnName = "Duration_Milliseconds", IsNullable = true, ColumnDescription = "执行耗时（毫秒）")]
    public long? DurationMilliseconds { get; set; }

    /// <summary>
    /// 归属租户
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "归属租户")]
    public long? TenantId { get; set; }

    /// <summary>
    /// 触发类型
    /// </summary>
    [SugarColumn(ColumnName = "Trigger_Type", IsNullable = false, ColumnDescription = "触发类型")]
    public int TriggerType { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [SugarColumn(ColumnName = "Is_Success", IsNullable = false, ColumnDescription = "是否成功")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [SugarColumn(ColumnName = "Error_Message", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "错误信息")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    [SugarColumn(ColumnName = "Stack_Trace", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "堆栈跟踪")]
    public string? StackTrace { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    [SugarColumn(ColumnName = "Retry_Count", IsNullable = false, ColumnDescription = "重试次数")]
    public int RetryCount { get; set; }

    /// <summary>
    /// 执行节点
    /// </summary>
    [SugarColumn(ColumnName = "Execution_Node", Length = 256, IsNullable = true, ColumnDescription = "执行节点")]
    public string? ExecutionNode { get; set; }

    /// <summary>
    /// 追踪标识
    /// </summary>
    [SugarColumn(ColumnName = "Trace_Id", Length = 64, IsNullable = true, ColumnDescription = "追踪标识")]
    public string? TraceId { get; set; }

    /// <summary>
    /// 执行参数的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Parameters_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "执行参数的 JSON")]
    public string? ParametersJson { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [SugarColumn(ColumnName = "Remarks", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "备注")]
    public string? Remarks { get; set; }
}
