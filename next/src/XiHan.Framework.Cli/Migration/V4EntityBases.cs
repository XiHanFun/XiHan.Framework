// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 4.x SqlSugar 实体基类的展开表（<c>spec/migration/v4.md</c> §4.2）：每个基类实现哪些协议接口、展开哪些属性；列名与列说明沿用 4.x。
/// </summary>
internal static class V4EntityBases
{
    /// <summary>
    /// 基类名（去掉泛型参数）→ (接口, 属性组)。<c>WithIdentity</c> 变体主键自增。
    /// </summary>
    private static readonly Dictionary<string, (string[] Interfaces, string[] Groups)> _bases = new(StringComparer.Ordinal)
    {
        ["SugarEntity"] = (["IEntity<TKey>", "IHasConcurrencyStamp"], ["id", "stamp"]),
        ["SugarEntityWithIdentity"] = (["IEntity<TKey>", "IHasConcurrencyStamp"], ["identity", "stamp"]),
        ["SugarCreationEntity"] = (["IEntity<TKey>", "IHasConcurrencyStamp", "IHasCreationTime"], ["id", "stamp", "creation"]),
        ["SugarCreationEntityWithIdentity"] = (["IEntity<TKey>", "IHasConcurrencyStamp", "IHasCreationTime"], ["identity", "stamp", "creation"]),
        ["SugarModificationEntity"] = (["IEntity<TKey>", "IHasConcurrencyStamp", "IHasCreationTime", "IHasModificationTime"], ["id", "stamp", "creation", "modification"]),
        ["SugarModificationEntityWithIdentity"] = (["IEntity<TKey>", "IHasConcurrencyStamp", "IHasCreationTime", "IHasModificationTime"], ["identity", "stamp", "creation", "modification"]),
        ["SugarDeletionEntity"] = (["IEntity<TKey>", "IHasConcurrencyStamp", "IHasCreationTime", "IHasModificationTime", "ISoftDeletable"], ["id", "stamp", "creation", "modification", "deletion"]),
        ["SugarDeletionEntityWithIdentity"] = (["IEntity<TKey>", "IHasConcurrencyStamp", "IHasCreationTime", "IHasModificationTime", "ISoftDeletable"], ["identity", "stamp", "creation", "modification", "deletion"]),
        ["SugarFullAuditedEntity"] = (["IEntity<TKey>", "IHasConcurrencyStamp", "IHasCreationTime", "IHasModificationTime", "ISoftDeletable"], ["id", "stamp", "creation", "modification", "deletion"]),
        ["SugarFullAuditedEntityWithIdentity"] = (["IEntity<TKey>", "IHasConcurrencyStamp", "IHasCreationTime", "IHasModificationTime", "ISoftDeletable"], ["identity", "stamp", "creation", "modification", "deletion"]),
        ["SugarAggregateRoot"] = (["IAggregateRoot<TKey>", "IHasConcurrencyStamp", "IHasCreationTime", "IHasModificationTime", "ISoftDeletable"], ["id", "stamp", "creation", "modification", "deletion", "events"]),
        ["SugarAggregateRootWithIdentity"] = (["IAggregateRoot<TKey>", "IHasConcurrencyStamp", "IHasCreationTime", "IHasModificationTime", "ISoftDeletable"], ["identity", "stamp", "creation", "modification", "deletion", "events"]),
    };

    /// <summary>
    /// 属性组模板：<c>TKey</c> 在生成时替换成主键类型，<c>{…}</c> 占位的文档注释按工具区域取文案；接口成员用 <c>inheritdoc</c>。
    /// </summary>
    private static readonly Dictionary<string, string> _groups = new(StringComparer.Ordinal)
    {
        ["id"] = """
            /// <inheritdoc />
            [SugarColumn(ColumnName = "Basic_Id", IsPrimaryKey = true, IsIdentity = false, ColumnDescription = "主键")]
            public virtual TKey Id { get; set; } = default!;
            """,
        ["identity"] = """
            /// <inheritdoc />
            [SugarColumn(ColumnName = "Basic_Id", IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "主键")]
            public virtual TKey Id { get; set; } = default!;
            """,
        ["stamp"] = """
            /// <inheritdoc />
            [SugarColumn(ColumnName = "Row_Version", Length = 32, ColumnDescription = "版本控制标识，用于处理并发")]
            public virtual string ConcurrencyStamp { get; set; } = string.Empty;
            """,
        ["creation"] = """
            /// <inheritdoc />
            [SugarColumn(ColumnName = "Created_Time", IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
            public virtual DateTimeOffset CreatedAt { get; set; }

            /// <summary>{created_id}</summary>
            [SugarColumn(ColumnName = "Created_Id", IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建者唯一标识")]
            public virtual TKey? CreatedId { get; set; }

            /// <summary>{created_by}</summary>
            [SugarColumn(ColumnName = "Created_By", IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建人")]
            public virtual string? CreatedBy { get; set; }
            """,
        ["modification"] = """
            /// <inheritdoc />
            [SugarColumn(ColumnName = "Modified_Time", IsNullable = true, ColumnDescription = "修改时间")]
            public virtual DateTimeOffset? ModifiedAt { get; set; }

            /// <summary>{modified_id}</summary>
            [SugarColumn(ColumnName = "Modified_Id", IsNullable = true, ColumnDescription = "修改者唯一标识")]
            public virtual TKey? ModifiedId { get; set; }

            /// <summary>{modified_by}</summary>
            [SugarColumn(ColumnName = "Modified_By", IsNullable = true, ColumnDescription = "修改人")]
            public virtual string? ModifiedBy { get; set; }
            """,
        ["deletion"] = """
            /// <inheritdoc />
            [SugarColumn(ColumnName = "Is_Deleted", IsNullable = false, ColumnDescription = "软删除标记")]
            public virtual bool IsDeleted { get; set; }

            /// <inheritdoc />
            [SugarColumn(ColumnName = "Deleted_Time", IsNullable = true, ColumnDescription = "删除时间")]
            public virtual DateTimeOffset? DeletedAt { get; set; }

            /// <summary>{deleted_id}</summary>
            [SugarColumn(ColumnName = "Deleted_Id", IsNullable = true, ColumnDescription = "删除者唯一标识")]
            public virtual TKey? DeletedId { get; set; }

            /// <summary>{deleted_by}</summary>
            [SugarColumn(ColumnName = "Deleted_By", IsNullable = true, ColumnDescription = "删除者")]
            public virtual string? DeletedBy { get; set; }
            """,
        ["tenant"] = """
            /// <inheritdoc />
            [SugarColumn(ColumnName = "Tenant_Id", IsOnlyIgnoreUpdate = true, ColumnDescription = "租户ID")]
            public virtual string TenantId { get; set; } = string.Empty;
            """,
        ["events"] = """
            /// <summary>{events}</summary>
            private readonly DomainEventBuffer _events = new();

            /// <inheritdoc />
            public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;

            /// <summary>{raise}</summary>
            public void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

            /// <inheritdoc />
            public void ClearDomainEvents() => _events.Clear();
            """,
    };

    /// <summary>
    /// 4.x 标记接口 → 5.0 接口；值为空表示没有对应物（保留为应用接口并留标记）。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string?> Interfaces = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["IMultiTenantEntity"] = "IMultiTenant",
        ["IStrictMultiTenantEntity"] = "IMultiTenant",
        ["ITraceableEntity"] = null,
        ["ISplitTableEntity"] = null,
    };

    /// <summary>
    /// 成员改名：4.x 名 → 5.0 名。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Renames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["BasicId"] = "Id",
        ["CreatedTime"] = "CreatedAt",
        ["ModifiedTime"] = "ModifiedAt",
        ["DeletedTime"] = "DeletedAt",
        ["RowVersion"] = "ConcurrencyStamp",
    };

    /// <summary>
    /// 解析 4.x 基类名（含 <c>SugarMultiTenant…</c> 变体）：返回接口列表与按 <paramref name="indent"/> 缩进的成员源码；不是 4.x 基类返回 <see langword="false"/>。
    /// </summary>
    public static bool TryExpand(string baseName, string keyType, string indent, string newLine, out IReadOnlyList<string> interfaces, out string members, out bool multiTenant)
    {
        multiTenant = baseName.StartsWith("SugarMultiTenant", StringComparison.Ordinal);
        var core = multiTenant ? "Sugar" + baseName["SugarMultiTenant".Length..] : baseName;
        if (!_bases.TryGetValue(core, out var entry))
        {
            interfaces = [];
            members = string.Empty;
            return false;
        }

        var list = entry.Interfaces.Select(i => i.Replace("TKey", keyType, StringComparison.Ordinal)).ToList();
        var groups = entry.Groups.ToList();
        if (multiTenant)
        {
            list.Add("IMultiTenant");
            groups.Add("tenant");
        }

        interfaces = list;
        members = string.Join(newLine, groups.Select(group => Render(_groups[group], keyType, indent, newLine)));
        return true;
    }

    /// <summary>
    /// 渲染一组模板：替换主键类型与文档占位，每行加缩进并统一换行；末尾带换行。
    /// </summary>
    private static string Render(string template, string keyType, string indent, string newLine)
    {
        var text = template
            .Replace("TKey", keyType, StringComparison.Ordinal)
            .Replace("{created_id}", CliMessages.MigrateEntityCreatedId.L(), StringComparison.Ordinal)
            .Replace("{created_by}", CliMessages.MigrateEntityCreatedBy.L(), StringComparison.Ordinal)
            .Replace("{modified_id}", CliMessages.MigrateEntityModifiedId.L(), StringComparison.Ordinal)
            .Replace("{modified_by}", CliMessages.MigrateEntityModifiedBy.L(), StringComparison.Ordinal)
            .Replace("{deleted_id}", CliMessages.MigrateEntityDeletedId.L(), StringComparison.Ordinal)
            .Replace("{deleted_by}", CliMessages.MigrateEntityDeletedBy.L(), StringComparison.Ordinal)
            .Replace("{events}", CliMessages.MigrateEntityEvents.L(), StringComparison.Ordinal)
            .Replace("{raise}", CliMessages.MigrateEntityRaise.L(), StringComparison.Ordinal);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return string.Concat(lines.Select(line => (line.Length == 0 ? string.Empty : indent + line) + newLine));
    }
}
