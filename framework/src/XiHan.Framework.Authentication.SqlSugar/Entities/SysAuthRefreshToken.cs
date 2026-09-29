// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Authentication.SqlSugar.Entities;

/// <summary>
/// 刷新令牌实体
/// </summary>
[SugarTable("sys_auth_refresh_token")]
[SugarIndex("ux_{table}_token_hash", nameof(TokenHash), OrderByType.Asc, true)]
[SugarIndex("ix_{table}_tenant_subject", nameof(TenantId), OrderByType.Asc, nameof(Subject), OrderByType.Asc)]
[SugarIndex("ix_{table}_expires_at", nameof(ExpiresAt), OrderByType.Asc)]
public class SysAuthRefreshToken : SugarEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthRefreshToken() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysAuthRefreshToken(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 签发时的租户标识，0 表示平台
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = false, ColumnDescription = "签发时的租户标识，0 表示平台")]
    public long TenantId { get; set; }

    /// <summary>
    /// 刷新令牌的 SHA-256 哈希
    /// </summary>
    [SugarColumn(ColumnName = "Token_Hash", Length = 64, IsNullable = false, ColumnDescription = "刷新令牌的 SHA-256 哈希")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// 主体标识
    /// </summary>
    [SugarColumn(ColumnName = "Subject", Length = 256, IsNullable = true, ColumnDescription = "主体标识")]
    public string? Subject { get; set; }

    /// <summary>
    /// 过期时间（UTC）
    /// </summary>
    [SugarColumn(ColumnName = "Expires_At", IsNullable = false, ColumnDescription = "过期时间（UTC）")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, ColumnDescription = "创建时间（UTC）")]
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// 撤销时间（UTC），为空表示未撤销
    /// </summary>
    [SugarColumn(ColumnName = "Revoked_Time", IsNullable = true, ColumnDescription = "撤销时间（UTC），为空表示未撤销")]
    public DateTime? RevokedTime { get; set; }
}
