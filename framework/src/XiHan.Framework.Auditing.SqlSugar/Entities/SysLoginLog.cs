// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Auditing.SqlSugar.Entities;

/// <summary>
/// 登录日志实体
/// </summary>
[SplitTable(SplitType.Month)]
[SugarTable("sys_login_log_{year}{month}{day}")]
public class SysLoginLog : SugarCreationEntity<long>, ISplitTableEntity
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysLoginLog() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysLoginLog(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 创建时间，同时作为分表字段
    /// </summary>
    [SplitField]
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    public override DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 跟踪标识
    /// </summary>
    [SugarColumn(ColumnName = "Trace_Id", Length = 64, IsNullable = true, ColumnDescription = "跟踪标识")]
    public string? TraceId { get; set; }

    /// <summary>
    /// 用户标识
    /// </summary>
    [SugarColumn(ColumnName = "User_Id", IsNullable = true, ColumnDescription = "用户标识")]
    public long? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [SugarColumn(ColumnName = "User_Name", Length = 128, IsNullable = true, ColumnDescription = "用户名")]
    public string? UserName { get; set; }

    /// <summary>
    /// 会话标识
    /// </summary>
    [SugarColumn(ColumnName = "Session_Id", Length = 64, IsNullable = true, ColumnDescription = "会话标识")]
    public string? SessionId { get; set; }

    /// <summary>
    /// 登录结果
    /// </summary>
    [SugarColumn(ColumnName = "Login_Result", IsNullable = false, ColumnDescription = "登录结果")]
    public int LoginResult { get; set; }

    /// <summary>
    /// 结果消息
    /// </summary>
    [SugarColumn(ColumnName = "Message", Length = 512, IsNullable = true, ColumnDescription = "结果消息")]
    public string? Message { get; set; }

    /// <summary>
    /// 登录地址
    /// </summary>
    [SugarColumn(ColumnName = "Login_Ip", Length = 64, IsNullable = true, ColumnDescription = "登录地址")]
    public string? LoginIp { get; set; }

    /// <summary>
    /// 用户代理
    /// </summary>
    [SugarColumn(ColumnName = "User_Agent", Length = 512, IsNullable = true, ColumnDescription = "用户代理")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 设备标识
    /// </summary>
    [SugarColumn(ColumnName = "Device_Id", Length = 128, IsNullable = true, ColumnDescription = "设备标识")]
    public string? DeviceId { get; set; }

    /// <summary>
    /// 登录时间
    /// </summary>
    [SugarColumn(ColumnName = "Login_Time", IsNullable = false, ColumnDescription = "登录时间")]
    public DateTimeOffset LoginTime { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? TenantId { get; set; }
}
