// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Auditing.SqlSugar.Entities;

/// <summary>
/// 接口日志实体
/// </summary>
[SplitTable(SplitType.Month)]
[SugarTable("sys_api_log_{year}{month}{day}")]
public class SysApiLog : SugarCreationEntity<long>, ISplitTableEntity
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysApiLog() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysApiLog(long basicId) : base(basicId)
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
    /// 客户端标识
    /// </summary>
    [SugarColumn(ColumnName = "Client_Id", Length = 128, IsNullable = true, ColumnDescription = "客户端标识")]
    public string? ClientId { get; set; }

    /// <summary>
    /// 应用标识
    /// </summary>
    [SugarColumn(ColumnName = "App_Id", Length = 128, IsNullable = true, ColumnDescription = "应用标识")]
    public string? AppId { get; set; }

    /// <summary>
    /// 签名是否有效
    /// </summary>
    [SugarColumn(ColumnName = "Is_Signature_Valid", IsNullable = false, ColumnDescription = "签名是否有效")]
    public bool IsSignatureValid { get; set; } = true;

    /// <summary>
    /// 签名算法
    /// </summary>
    [SugarColumn(ColumnName = "Signature_Algorithm", Length = 64, IsNullable = true, ColumnDescription = "签名算法")]
    public string? SignatureAlgorithm { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    [SugarColumn(ColumnName = "Method", Length = 16, IsNullable = false, ColumnDescription = "请求方法")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 请求路径
    /// </summary>
    [SugarColumn(ColumnName = "Path", Length = 512, IsNullable = false, ColumnDescription = "请求路径")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 接口名称
    /// </summary>
    [SugarColumn(ColumnName = "Api_Name", Length = 256, IsNullable = true, ColumnDescription = "接口名称")]
    public string? ApiName { get; set; }

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
    /// 响应体
    /// </summary>
    [SugarColumn(ColumnName = "Response_Body", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "响应体")]
    public string? ResponseBody { get; set; }

    /// <summary>
    /// 状态码
    /// </summary>
    [SugarColumn(ColumnName = "Status_Code", IsNullable = false, ColumnDescription = "状态码")]
    public int StatusCode { get; set; }

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
    /// 来源页面
    /// </summary>
    [SugarColumn(ColumnName = "Referer", Length = 512, IsNullable = true, ColumnDescription = "来源页面")]
    public string? Referer { get; set; }

    /// <summary>
    /// 耗时毫秒
    /// </summary>
    [SugarColumn(ColumnName = "Elapsed_Milliseconds", IsNullable = false, ColumnDescription = "耗时毫秒")]
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 请求大小
    /// </summary>
    [SugarColumn(ColumnName = "Request_Size", IsNullable = false, ColumnDescription = "请求大小")]
    public long RequestSize { get; set; }

    /// <summary>
    /// 响应大小
    /// </summary>
    [SugarColumn(ColumnName = "Response_Size", IsNullable = false, ColumnDescription = "响应大小")]
    public long ResponseSize { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [SugarColumn(ColumnName = "Is_Success", IsNullable = false, ColumnDescription = "是否成功")]
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// 错误信息
    /// </summary>
    [SugarColumn(ColumnName = "Error_Message", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "错误信息")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? TenantId { get; set; }
}
