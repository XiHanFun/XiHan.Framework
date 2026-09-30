// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Entities;

/// <summary>
/// 定时任务实例实体，时间均为协调世界时
/// </summary>
[SugarTable("sys_job_instance")]
[SugarIndex("idx_sys_job_instance_name_status",
    nameof(SysJobInstance.JobName), OrderByType.Asc,
    nameof(SysJobInstance.Status), OrderByType.Asc)]
[SugarIndex("idx_sys_job_instance_completed_at", nameof(SysJobInstance.CompletedAt), OrderByType.Asc)]
public class SysJobInstance : SugarEntity<string>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysJobInstance() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">实例唯一标识</param>
    public SysJobInstance(string basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 任务名称
    /// </summary>
    [SugarColumn(ColumnName = "Job_Name", Length = 256, IsNullable = false, ColumnDescription = "任务名称")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 任务类型的程序集限定名
    /// </summary>
    [SugarColumn(ColumnName = "Job_Type_Name", Length = 512, IsNullable = true, ColumnDescription = "任务类型的程序集限定名")]
    public string? JobTypeName { get; set; }

    /// <summary>
    /// 任务状态
    /// </summary>
    [SugarColumn(ColumnName = "Status", IsNullable = false, ColumnDescription = "任务状态")]
    public int Status { get; set; }

    /// <summary>
    /// 触发类型
    /// </summary>
    [SugarColumn(ColumnName = "Trigger_Type", IsNullable = false, ColumnDescription = "触发类型")]
    public int TriggerType { get; set; }

    /// <summary>
    /// 归属租户
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "归属租户")]
    public long? TenantId { get; set; }

    /// <summary>
    /// 计划执行时间
    /// </summary>
    [SugarColumn(ColumnName = "Scheduled_At", IsNullable = false, ColumnDescription = "计划执行时间")]
    public DateTime ScheduledAt { get; set; }

    /// <summary>
    /// 实际开始时间
    /// </summary>
    [SugarColumn(ColumnName = "Started_At", IsNullable = true, ColumnDescription = "实际开始时间")]
    public DateTime? StartedAt { get; set; }

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
    /// 运行截止时刻，仅运行中状态有值
    /// </summary>
    [SugarColumn(ColumnName = "Running_Deadline", IsNullable = true, ColumnDescription = "运行截止时刻，仅运行中状态有值")]
    public DateTime? RunningDeadline { get; set; }

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
    /// 错误信息
    /// </summary>
    [SugarColumn(ColumnName = "Error_Message", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "错误信息")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    [SugarColumn(ColumnName = "Stack_Trace", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "堆栈跟踪")]
    public string? StackTrace { get; set; }
}
