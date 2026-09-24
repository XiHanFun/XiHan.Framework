// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Aggregates;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 实体基类的公共列在库里同名：同一个概念（主键、租户、审计、软删）不能因为基类不同而有两种列名
/// </summary>
/// <remarks>
/// 起因：聚合根基类的公共列没声明列名，落库成 basicid / tenantid / isdeleted，其余基类是 basic_id / tenant_id / is_deleted。
/// 手写的升级脚本按一种写、撞上另一种，新库启动直接失败。
/// </remarks>
public sealed class EntityBaseColumnNamingTests : IDisposable
{
    /// <summary>
    /// 公共属性 → 规范列名
    /// </summary>
    private static readonly Dictionary<string, string> CanonicalColumns = new(StringComparer.Ordinal)
    {
        ["BasicId"] = "Basic_Id",
        ["RowVersion"] = "Row_Version",
        ["TenantId"] = "Tenant_Id",
        ["CreatedTime"] = "Created_Time",
        ["CreatedId"] = "Created_Id",
        ["CreatedBy"] = "Created_By",
        ["ModifiedTime"] = "Modified_Time",
        ["ModifiedId"] = "Modified_Id",
        ["ModifiedBy"] = "Modified_By",
        ["IsDeleted"] = "Is_Deleted",
        ["DeletedTime"] = "Deleted_Time",
        ["DeletedId"] = "Deleted_Id",
        ["DeletedBy"] = "Deleted_By"
    };

    private readonly SqlSugarClient _client = new(new ConnectionConfig
    {
        DbType = DbType.PostgreSQL,
        ConnectionString = "Host=unused",
        IsAutoCloseConnection = true
    });

    public static TheoryData<Type> BaseEntityTypes =>
    [
        typeof(EntityProbe),
        typeof(IdentityEntityProbe),
        typeof(CreationProbe),
        typeof(ModificationProbe),
        typeof(DeletionProbe),
        typeof(FullAuditedProbe),
        typeof(AggregateRootProbe),
        typeof(MultiTenantEntityProbe),
        typeof(MultiTenantIdentityEntityProbe),
        typeof(MultiTenantCreationProbe),
        typeof(MultiTenantModificationProbe),
        typeof(MultiTenantDeletionProbe),
        typeof(MultiTenantFullAuditedProbe),
        typeof(MultiTenantAggregateRootProbe)
    ];

    [Theory]
    [MemberData(nameof(BaseEntityTypes))]
    public void 公共列按规范列名落库(Type entityType)
    {
        var columns = _client.EntityMaintenance.GetEntityInfo(entityType).Columns
            .Where(column => !column.IsIgnore && CanonicalColumns.ContainsKey(column.PropertyName))
            .ToList();

        Assert.NotEmpty(columns);
        Assert.All(columns, column => Assert.Equal(CanonicalColumns[column.PropertyName], column.DbColumnName));
    }

    /// <summary>
    /// 释放客户端
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
    }

    private sealed class EntityProbe : SugarEntity<long>;

    private sealed class IdentityEntityProbe : SugarEntityWithIdentity<long>;

    private sealed class CreationProbe : SugarCreationEntity<long>;

    private sealed class ModificationProbe : SugarModificationEntity<long>;

    private sealed class DeletionProbe : SugarDeletionEntity<long>;

    private sealed class FullAuditedProbe : SugarFullAuditedEntity<long>;

    private sealed class AggregateRootProbe : SugarAggregateRoot<long>;

    private sealed class MultiTenantEntityProbe : SugarMultiTenantEntity<long>;

    private sealed class MultiTenantIdentityEntityProbe : SugarMultiTenantEntityWithIdentity<long>;

    private sealed class MultiTenantCreationProbe : SugarMultiTenantCreationEntity<long>;

    private sealed class MultiTenantModificationProbe : SugarMultiTenantModificationEntity<long>;

    private sealed class MultiTenantDeletionProbe : SugarMultiTenantDeletionEntity<long>;

    private sealed class MultiTenantFullAuditedProbe : SugarMultiTenantFullAuditedEntity<long>;

    private sealed class MultiTenantAggregateRootProbe : SugarMultiTenantAggregateRoot<long>;
}
