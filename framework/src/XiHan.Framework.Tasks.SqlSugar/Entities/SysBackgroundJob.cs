// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Entities;

/// <summary>
/// 后台作业实体
/// </summary>
[SugarTable("sys_background_job")]
[SugarIndex("idx_sys_background_job_waiting",
    nameof(SysBackgroundJob.ApplicationName), OrderByType.Asc,
    nameof(SysBackgroundJob.IsAbandoned), OrderByType.Asc,
    nameof(SysBackgroundJob.NextTryTime), OrderByType.Asc)]
[SugarIndex("idx_sys_background_job_claim_token",
    nameof(SysBackgroundJob.ClaimToken), OrderByType.Asc)]
public class SysBackgroundJob : SugarEntity<Guid>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysBackgroundJob() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">作业唯一标识</param>
    public SysBackgroundJob(Guid basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 入队应用名称，空字符串表示未指定
    /// </summary>
    [SugarColumn(ColumnName = "Application_Name", Length = 128, IsNullable = false, ColumnDescription = "入队应用名称，空字符串表示未指定")]
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// 入队时的租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "入队时的租户标识")]
    public long? TenantId { get; set; }

    /// <summary>
    /// 作业名称
    /// </summary>
    [SugarColumn(ColumnName = "Job_Name", Length = 256, IsNullable = false, ColumnDescription = "作业名称")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 序列化后的作业参数
    /// </summary>
    [SugarColumn(ColumnName = "Job_Args", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "序列化后的作业参数")]
    public string JobArgs { get; set; } = string.Empty;

    /// <summary>
    /// 已尝试次数
    /// </summary>
    [SugarColumn(ColumnName = "Try_Count", IsNullable = false, ColumnDescription = "已尝试次数")]
    public short TryCount { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnName = "Creation_Time", IsNullable = false, ColumnDescription = "创建时间")]
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 下次可执行时间
    /// </summary>
    [SugarColumn(ColumnName = "Next_Try_Time", IsNullable = false, ColumnDescription = "下次可执行时间")]
    public DateTime NextTryTime { get; set; }

    /// <summary>
    /// 上次尝试时间
    /// </summary>
    [SugarColumn(ColumnName = "Last_Try_Time", IsNullable = true, ColumnDescription = "上次尝试时间")]
    public DateTime? LastTryTime { get; set; }

    /// <summary>
    /// 是否已放弃
    /// </summary>
    [SugarColumn(ColumnName = "Is_Abandoned", IsNullable = false, ColumnDescription = "是否已放弃")]
    public bool IsAbandoned { get; set; }

    /// <summary>
    /// 优先级，值越大越优先
    /// </summary>
    [SugarColumn(ColumnName = "Priority", IsNullable = false, ColumnDescription = "优先级，值越大越优先")]
    public int Priority { get; set; }

    /// <summary>
    /// 领取令牌
    /// </summary>
    [SugarColumn(ColumnName = "Claim_Token", Length = 64, IsNullable = true, ColumnDescription = "领取令牌")]
    public string? ClaimToken { get; set; }

    /// <summary>
    /// 领取时刻
    /// </summary>
    [SugarColumn(ColumnName = "Claim_Time", IsNullable = true, ColumnDescription = "领取时刻")]
    public DateTime? ClaimTime { get; set; }

    /// <summary>
    /// 是否已请求取消，空值与 false 均表示未请求
    /// </summary>
    [SugarColumn(ColumnName = "Is_Cancellation_Requested", IsNullable = true, ColumnDescription = "是否已请求取消")]
    public bool? IsCancellationRequested { get; set; }
}
