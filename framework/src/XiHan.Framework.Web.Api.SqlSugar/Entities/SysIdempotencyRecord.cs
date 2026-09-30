// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.Web.Api.SqlSugar.Entities;

/// <summary>
/// 接口幂等记录实体
/// </summary>
[SugarTable("sys_idempotency_record")]
[SugarIndex("ux_sys_idempotency_record_key_hash", nameof(KeyHash), OrderByType.Asc, true)]
[TableInitialization]
public class SysIdempotencyRecord : SugarEntity<Guid>
{
    /// <summary>
    /// 处理中状态
    /// </summary>
    public const int StatusProcessing = 0;

    /// <summary>
    /// 已完成状态
    /// </summary>
    public const int StatusCompleted = 1;

    /// <summary>
    /// 结果不确定状态
    /// </summary>
    public const int StatusIndeterminate = 2;

    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysIdempotencyRecord() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">记录唯一标识</param>
    public SysIdempotencyRecord(Guid basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 记录键摘要（租户、主体、方法、端点、幂等键的 SHA-256）
    /// </summary>
    [SugarColumn(ColumnName = "Key_Hash", Length = 64, IsNullable = false, ColumnDescription = "记录键摘要")]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// 租户标识，宿主为空字符串
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", Length = 64, IsNullable = false, ColumnDescription = "租户标识")]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// 调用主体标识
    /// </summary>
    [SugarColumn(ColumnName = "Subject_Id", Length = 128, IsNullable = false, ColumnDescription = "调用主体标识")]
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 方法
    /// </summary>
    [SugarColumn(ColumnName = "Http_Method", Length = 16, IsNullable = false, ColumnDescription = "HTTP 方法")]
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// 请求路径，超过列长度时截断
    /// </summary>
    [SugarColumn(ColumnName = "Endpoint", Length = 512, IsNullable = false, ColumnDescription = "请求路径")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 调用方提供的幂等键
    /// </summary>
    [SugarColumn(ColumnName = "Idempotency_Key", Length = 128, IsNullable = false, ColumnDescription = "幂等键")]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// 请求摘要
    /// </summary>
    [SugarColumn(ColumnName = "Fingerprint", Length = 64, IsNullable = false, ColumnDescription = "请求摘要")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    /// 状态，0 处理中，1 已完成，2 结果不确定
    /// </summary>
    [SugarColumn(ColumnName = "Status", IsNullable = false, ColumnDescription = "状态，0 处理中，1 已完成，2 结果不确定")]
    public int Status { get; set; }

    /// <summary>
    /// 拥有者令牌
    /// </summary>
    [SugarColumn(ColumnName = "Owner_Token", IsNullable = false, ColumnDescription = "拥有者令牌")]
    public Guid OwnerToken { get; set; }

    /// <summary>
    /// 动作是否在事务型工作单元内执行
    /// </summary>
    [SugarColumn(ColumnName = "Is_Transactional", IsNullable = false, ColumnDescription = "是否事务型")]
    public bool IsTransactional { get; set; }

    /// <summary>
    /// 处理中租约到期时间
    /// </summary>
    [SugarColumn(ColumnName = "Lease_Expires_Time", IsNullable = false, ColumnDescription = "处理中租约到期时间")]
    public DateTimeOffset LeaseExpiresTime { get; set; }

    /// <summary>
    /// 完成或不确定记录过期时间
    /// </summary>
    [SugarColumn(ColumnName = "Expires_Time", IsNullable = true, ColumnDescription = "完成或不确定记录过期时间")]
    public DateTimeOffset? ExpiresTime { get; set; }

    /// <summary>
    /// 响应状态码
    /// </summary>
    [SugarColumn(ColumnName = "Response_Status", IsNullable = true, ColumnDescription = "响应状态码")]
    public int? ResponseStatus { get; set; }

    /// <summary>
    /// 响应快照（动作返回值 JSON）
    /// </summary>
    [SugarColumn(ColumnName = "Response_Body", IsNullable = true, ColumnDescription = "响应快照")]
    public byte[]? ResponseBody { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, ColumnDescription = "创建时间")]
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    [SugarColumn(ColumnName = "Completed_Time", IsNullable = true, ColumnDescription = "完成时间")]
    public DateTimeOffset? CompletedTime { get; set; }
}
