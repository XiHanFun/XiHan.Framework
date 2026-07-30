// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 实体差异日志记录
/// </summary>
public class EntityDiffLogRecord
{
    /// <summary>
    /// 审计类型
    /// </summary>
    public string AuditType { get; set; } = "EntityChange";

    /// <summary>
    /// 操作类型（Create/Update/Delete）
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// 实体类型
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// 实体标识
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// 前值 JSON
    /// </summary>
    public string? BeforeData { get; set; }

    /// <summary>
    /// 后值 JSON
    /// </summary>
    public string? AfterData { get; set; }

    /// <summary>
    /// 变更字段 JSON
    /// </summary>
    public string? ChangedFields { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? RequestMethod { get; set; }

    /// <summary>
    /// 操作 IP
    /// </summary>
    public string? OperationIp { get; set; }

    /// <summary>
    /// 请求标识
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// 用户标识
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public long? TenantId { get; set; }
}
