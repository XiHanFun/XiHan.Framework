// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Auditing.SqlSugar.Entities;

/// <summary>
/// 异常日志实体
/// </summary>
[SplitTable(SplitType.Month)]
[SugarTable("sys_exception_log_{year}{month}{day}")]
public class SysExceptionLog : SugarCreationEntity<long>, ISplitTableEntity
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysExceptionLog() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysExceptionLog(long basicId) : base(basicId)
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
    [SugarColumn(ColumnName = "Trace_Id", Length = 64, IsNullable = false, ColumnDescription = "跟踪标识")]
    public string TraceId { get; set; } = string.Empty;

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
    /// 请求路径
    /// </summary>
    [SugarColumn(ColumnName = "Path", Length = 512, IsNullable = true, ColumnDescription = "请求路径")]
    public string? Path { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    [SugarColumn(ColumnName = "Method", Length = 16, IsNullable = true, ColumnDescription = "请求方法")]
    public string? Method { get; set; }

    /// <summary>
    /// 控制器
    /// </summary>
    [SugarColumn(ColumnName = "Controller_Name", Length = 256, IsNullable = true, ColumnDescription = "控制器")]
    public string? ControllerName { get; set; }

    /// <summary>
    /// 动作
    /// </summary>
    [SugarColumn(ColumnName = "Action_Name", Length = 256, IsNullable = true, ColumnDescription = "动作")]
    public string? ActionName { get; set; }

    /// <summary>
    /// 状态码
    /// </summary>
    [SugarColumn(ColumnName = "Status_Code", IsNullable = false, ColumnDescription = "状态码")]
    public int StatusCode { get; set; }

    /// <summary>
    /// 异常类型
    /// </summary>
    [SugarColumn(ColumnName = "Exception_Type", Length = 512, IsNullable = false, ColumnDescription = "异常类型")]
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// 异常消息
    /// </summary>
    [SugarColumn(ColumnName = "Exception_Message", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "异常消息")]
    public string ExceptionMessage { get; set; } = string.Empty;

    /// <summary>
    /// 异常堆栈
    /// </summary>
    [SugarColumn(ColumnName = "Exception_Stack_Trace", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "异常堆栈")]
    public string? ExceptionStackTrace { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    [SugarColumn(ColumnName = "Request_Headers", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "请求头")]
    public string? RequestHeaders { get; set; }

    /// <summary>
    /// 请求参数
    /// </summary>
    [SugarColumn(ColumnName = "Request_Params", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "请求参数")]
    public string? RequestParams { get; set; }

    /// <summary>
    /// 请求体
    /// </summary>
    [SugarColumn(ColumnName = "Request_Body", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "请求体")]
    public string? RequestBody { get; set; }

    /// <summary>
    /// 来源地址
    /// </summary>
    [SugarColumn(ColumnName = "Remote_Ip", Length = 64, IsNullable = true, ColumnDescription = "来源地址")]
    public string? RemoteIp { get; set; }

    /// <summary>
    /// 用户代理
    /// </summary>
    [SugarColumn(ColumnName = "User_Agent", Length = 512, IsNullable = true, ColumnDescription = "用户代理")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? TenantId { get; set; }
}
