// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 一条权限授予的行：主体种类、主体 ID、权限码与租户共同唯一。平台级数据，不实现 <see cref="IMultiTenant"/>（授予表自带租户列，不受当前调用租户过滤），不软删除。
/// 全局授予的 <see cref="TenantId"/> 存 <see cref="GlobalTenantId"/>（空字符串）而不是 NULL：唯一索引对 NULL 的语义各数据库不一致，空字符串在哪里都参与唯一性；
/// 契约边界（<see cref="PermissionGrant"/>）仍以 <see langword="null"/> 表示全局，经 <see cref="ToGrant"/> 换算。
/// EF Core 应用在自己的 <c>DbContext</c> 里 <c>modelBuilder.Entity&lt;PermissionGrantRecord&gt;()</c> 并建唯一索引。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class PermissionGrantRecord : IEntity<Guid>, IHasCreationTime
{
    /// <summary>
    /// 全局授予在 <see cref="TenantId"/> 列里的值：空字符串。
    /// </summary>
    public const string GlobalTenantId = "";

    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 主体种类：<see cref="GrantSubject.User"/> 或 <see cref="GrantSubject.Role"/>。
    /// </summary>
    [StringLength(32)]
    public string SubjectKind { get; set; } = string.Empty;

    /// <summary>
    /// 主体 ID（用户 ID 或角色名，区分大小写）。
    /// </summary>
    [StringLength(256)]
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// 权限码，或 <see cref="PermissionGrant.All"/>。
    /// </summary>
    [StringLength(256)]
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// 租户；全局授予为 <see cref="GlobalTenantId"/>。
    /// </summary>
    [StringLength(128)]
    public string TenantId { get; set; } = GlobalTenantId;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 换算成契约里的授予：空字符串租户换成 <see langword="null"/>。
    /// </summary>
    public PermissionGrant ToGrant() => new(new GrantSubject(SubjectKind, SubjectId), Permission, TenantId.Length == 0 ? null : TenantId);

    /// <summary>
    /// 契约里的租户换算成列值：<see langword="null"/> 换成 <see cref="GlobalTenantId"/>。
    /// </summary>
    public static string TenantColumnOf(string? tenantId) => tenantId ?? GlobalTenantId;
}
