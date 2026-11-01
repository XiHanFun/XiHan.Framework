// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Text;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 软删除实体的部分唯一索引：只约束未删除的行（<see cref="EntityIndex.ExcludesDeleted"/>）。
/// </summary>
/// <remarks>
/// SqlSugar 的索引入口（<see cref="SugarIndexAttribute"/>、<c>DbMaintenance.CreateIndex</c>）写不出条件，部分唯一索引不交给它，由这里在建表之后按数据库方言建出。只支持 PostgreSQL、SQLite、SQL Server；其余数据库没有部分索引（MySQL 系、Oracle 系、达梦等）或没有验证过（源自 PostgreSQL 的人大金仓、openGauss、GaussDB、瀚高、Vastbase、PolarDB 等），一律按不支持处理、启动失败，不退化成普通唯一索引。
/// </remarks>
internal static class PartialUniqueIndexes
{
    /// <summary>
    /// 支持部分唯一索引、并在真实库上验证过的数据库类型。
    /// </summary>
    private static readonly DbType[] _supported = [DbType.PostgreSQL, DbType.Sqlite, DbType.SqlServer];

    /// <summary>
    /// 数据库类型是否支持部分唯一索引。
    /// </summary>
    public static bool IsSupported(DbType dbType) => Array.IndexOf(_supported, dbType) >= 0;

    /// <summary>
    /// 实体有没有部分唯一索引：软删除实体声明了唯一索引、列里没有删除标记。
    /// </summary>
    /// <remarks>
    /// 只看声明，不取实体信息、不推名字，启动核对时不必为每个实体建映射。
    /// </remarks>
    public static bool Declares(Type entityType)
        => typeof(ISoftDeletable).IsAssignableFrom(entityType)
            && entityType.GetCustomAttributes<EntityIndexAttribute>(inherit: true).Any(static index => index.IsUnique && !index.Properties.Contains(nameof(ISoftDeletable.IsDeleted), StringComparer.Ordinal));

    /// <summary>
    /// 实体的部分唯一索引，按名字排序。
    /// </summary>
    /// <remarks>
    /// <paramref name="table"/> 是映射出的表名，参与索引名。
    /// </remarks>
    public static IReadOnlyList<EntityIndex> Of(Type entityType, string table)
        => [.. EntityIndexes.Resolve(entityType, table).Where(static index => index.ExcludesDeleted)];

    /// <summary>
    /// 数据库不支持部分唯一索引时的启动错误，一次列出全部实体。
    /// </summary>
    public static FrameworkException NotSupported(IEnumerable<(Type EntityType, DbType DbType)> offenders)
    {
        var list = string.Join(", ", offenders.Select(static o => $"'{o.EntityType.FullName}' on {o.DbType}"));
        return new FrameworkException(
            PersistenceErrors.NotSupported,
            $"Soft-deletable entities with unique indexes need partial unique indexes (only rows with 'IsDeleted' = false), which this database does not support: {list}. Supported databases: PostgreSQL, SQLite, SQL Server. Declare 'IsDeleted' among the index properties to keep a plain unique index (at most one deleted row per key), or drop IsUnique.");
    }

    /// <summary>
    /// 在连接上补建实体的部分唯一索引。
    /// </summary>
    /// <remarks>
    /// 已有同名的索引不改、不删。
    /// </remarks>
    /// <exception cref="FrameworkException">数据库类型不支持部分唯一索引（<see cref="PersistenceErrors.NotSupported"/>）。</exception>
    public static void Create(ISqlSugarClient client, Type entityType)
    {
        var entity = client.EntityMaintenance.GetEntityInfo(entityType);
        var indexes = Of(entityType, entity.DbTableName);
        if (indexes.Count == 0)
        {
            return;
        }

        var dbType = client.CurrentConnectionConfig.DbType;
        if (!IsSupported(dbType))
        {
            throw NotSupported([(entityType, dbType)]);
        }

        // 引用表名与列名用 SqlSugar 自己的转义：PostgreSQL 的自动转小写等设置与它建表时一致
        var builder = InstanceFactory.GetSqlBuilderWithContext(client);
        foreach (var index in indexes)
        {
            client.Ado.ExecuteCommand(Sql(dbType, builder, entity, index));
        }
    }

    /// <summary>
    /// 建一个部分唯一索引的语句，已有同名索引时什么也不做。
    /// </summary>
    /// <remarks>
    /// PostgreSQL、SQLite 用 <c>CREATE UNIQUE INDEX IF NOT EXISTS … WHERE</c>，PostgreSQL 上名字转小写（与 SqlSugar 建出的其他索引一致，PostgreSQL 把不加引号的名字折成小写）；SQL Server 先查 <c>sys.indexes</c>，筛选索引的条件里给可空的键列另加 <c>IS NOT NULL</c>（SQL Server 的唯一索引把 NULL 当作相等，与 EF Core 在 SQL Server 上的缺省一致）。
    /// </remarks>
    internal static string Sql(DbType dbType, ISqlBuilder builder, EntityInfo entity, EntityIndex index)
    {
        var table = builder.GetTranslationTableName(entity.DbTableName);
        var keys = index.Properties.Select(property => ColumnOf(entity, property)).ToList();
        var columns = string.Join(", ", keys.Select(key => builder.GetTranslationColumnName(key.DbColumnName)));
        var deleted = builder.GetTranslationColumnName(ColumnOf(entity, nameof(ISoftDeletable.IsDeleted)).DbColumnName);
        switch (dbType)
        {
            case DbType.PostgreSQL:
                return $"CREATE UNIQUE INDEX IF NOT EXISTS \"{index.Name.ToLowerInvariant()}\" ON {table} ({columns}) WHERE {deleted} = false";
            case DbType.Sqlite:
                return $"CREATE UNIQUE INDEX IF NOT EXISTS {builder.GetTranslationColumnName(index.Name)} ON {table} ({columns}) WHERE {deleted} = 0";
            case DbType.SqlServer:
                var filter = new StringBuilder($"{deleted} = 0");
                foreach (var key in keys.Where(static key => key.IsNullable))
                {
                    filter.Append(" AND ").Append(builder.GetTranslationColumnName(key.DbColumnName)).Append(" IS NOT NULL");
                }

                // SQL Server 的索引名按表唯一：按名字与表一起查，查到即跳过
                return $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{Literal(index.Name)}' AND object_id = OBJECT_ID(N'{Literal(table)}')) CREATE UNIQUE NONCLUSTERED INDEX {builder.GetTranslationColumnName(index.Name)} ON {table} ({columns}) WHERE {filter}";
            default:
                throw new ArgumentOutOfRangeException(nameof(dbType), dbType, "Partial unique indexes are created on PostgreSQL, SQLite and SQL Server only.");
        }
    }

    /// <summary>
    /// 属性对应的列。
    /// </summary>
    private static EntityColumnInfo ColumnOf(EntityInfo entity, string property)
        => entity.Columns.Find(column => string.Equals(column.PropertyName, property, StringComparison.Ordinal) && !column.IsIgnore)
            ?? throw new InvalidOperationException($"Entity '{entity.Type.FullName}' has no mapped column for property '{property}' of index.");

    /// <summary>
    /// 放进 SQL 字符串字面量的文本：单引号成对。
    /// </summary>
    private static string Literal(string text) => text.Replace("'", "''", StringComparison.Ordinal);
}
