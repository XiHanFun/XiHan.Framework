// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 按实体协议配置模型。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ModelBuilderExtensions
{
    /// <summary>
    /// SQLite 的 EF Core Provider 名。
    /// </summary>
    internal const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    /// <summary>
    /// SQL Server 的 EF Core Provider 名。
    /// </summary>
    internal const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>
    /// PostgreSQL（Npgsql）的 EF Core Provider 名。
    /// </summary>
    internal const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>
    /// 对已注册的每个实体。
    /// </summary>
    /// <remarks>
    /// <see cref="IHasConcurrencyStamp"/> 的并发戳设为并发令牌（长度 32）；<see cref="IMultiTenant"/> 的租户与 <see cref="ISoftDeletable"/> 的删除标记建索引；实体上的 <see cref="EntityIndexAttribute"/> 映射为 <c>HasIndex</c>（唯一性与索引名按 <see cref="EntityIndexes"/>，名字用此刻的表名推出，与 SqlSugar 建出的同名）。软删除实体的唯一索引只约束未删除的行（<see cref="EntityIndex.ExcludesDeleted"/>），另加 <c>HasFilter</c>：条件按 <paramref name="database"/> 的 Provider 写方言（Npgsql <c>"IsDeleted" = false</c>、SQLite <c>"IsDeleted" = 0</c>、SQL Server <c>[IsDeleted] = 0</c>，SQL Server 上键含可空列时另加 <c>IS NOT NULL</c>，与它对唯一索引的缺省一致），列名取此刻的映射；其余关系型 Provider 不支持部分唯一索引，建模时抛出；非关系型 Provider 本来不建索引，不加条件。在 <c>OnModelCreating</c> 里注册全部实体、配置完表名与列名之后调用：<c>modelBuilder.ApplyPersistenceConventions(Database)</c>。
    /// </remarks>
    /// <param name="modelBuilder">模型构建器。</param>
    /// <param name="database">本 DbContext 的 <see cref="DbContext.Database"/>：据它的 Provider 写部分唯一索引的条件。</param>
    /// <exception cref="InvalidOperationException">实体的索引声明不合法（见 <see cref="EntityIndexes"/>）。</exception>
    /// <exception cref="FrameworkException">有软删除实体声明了唯一索引，而 Provider 不支持部分唯一索引（<see cref="PersistenceErrors.NotSupported"/>，一次列出全部）。</exception>
    public static ModelBuilder ApplyPersistenceConventions(this ModelBuilder modelBuilder, DatabaseFacade database)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(database);
        return Apply(modelBuilder, database.ProviderName, database.IsRelational());
    }

    /// <summary>
    /// 按 Provider 名施加约定。
    /// </summary>
    /// <remarks>
    /// 公开入口与测试共用：测试据此覆盖没有引用的 Provider（Npgsql、SQL Server、不支持的）。
    /// </remarks>
    internal static ModelBuilder Apply(ModelBuilder modelBuilder, string? providerName, bool relational)
    {
        // 不支持部分唯一索引的声明一次列出，不在第一个上就停
        var unsupported = new List<string>();
        // 先物化实体类型列表：配置属性与索引可能修改模型，不能一边枚举一边改
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;
            // 并发戳由 PersistenceConventions.NewStamp 生成，是 32 位十六进制，长度与之对齐
            if (typeof(IHasConcurrencyStamp).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp)).IsConcurrencyToken().HasMaxLength(32);
            }

            if (typeof(IMultiTenant).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasIndex(nameof(IMultiTenant.TenantId));
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasIndex(nameof(ISoftDeletable.IsDeleted));
            }

            // 从属类型与所有者共用一张表，不能单独作为实体配置；共享类型实体（属性包）没有可声明索引的 CLR 类型
            if (entityType.IsOwned() || entityType.HasSharedClrType)
            {
                continue;
            }

            // 索引名按表名推出：表名在这里取，ToTable 要在本方法之前配置
            foreach (var index in EntityIndexes.Resolve(clrType, entityType.GetTableName() ?? clrType.Name))
            {
                var builder = modelBuilder.Entity(clrType).HasIndex([.. index.Properties]).IsUnique(index.IsUnique).HasDatabaseName(index.Name);
                // 非关系型 Provider 不建索引，条件无从谈起；关系型的按方言写条件，写不出就记下、建模结束时一起报
                if (!index.ExcludesDeleted || !relational)
                {
                    continue;
                }

                if (FilterOf(providerName, entityType, index) is { } filter)
                {
                    builder.HasFilter(filter);
                }
                else
                {
                    unsupported.Add($"'{clrType.FullName}' ({index.Name})");
                }
            }
        }

        if (unsupported.Count > 0)
        {
            throw new FrameworkException(
                PersistenceErrors.NotSupported,
                $"Soft-deletable entities with unique indexes need partial unique indexes (only rows with 'IsDeleted' = false), which database provider '{providerName}' does not support: {string.Join(", ", unsupported)}. Supported providers: '{NpgsqlProvider}', '{SqliteProvider}', '{SqlServerProvider}'. Declare 'IsDeleted' among the index properties to keep a plain unique index (at most one deleted row per key), or drop IsUnique.");
        }

        return modelBuilder;
    }

    /// <summary>
    /// 部分唯一索引的条件：只约束未删除的行。
    /// </summary>
    /// <remarks>
    /// Provider 不支持时为 <see langword="null"/>。列名取此刻的映射（<c>HasColumnName</c> 与命名约定要在约定之前生效）。
    /// </remarks>
    internal static string? FilterOf(string? providerName, IMutableEntityType entityType, EntityIndex index)
    {
        var deleted = entityType.FindProperty(nameof(ISoftDeletable.IsDeleted))!.GetColumnName();
        switch (providerName)
        {
            case NpgsqlProvider:
                return $"{Quote(deleted)} = false";
            case SqliteProvider:
                return $"{Quote(deleted)} = 0";
            case SqlServerProvider:
                // SQL Server 的唯一索引把 NULL 当作相等：显式给了条件后它不再自动加 IS NOT NULL，这里补上，含 NULL 的键不受约束（与其他数据库一致）
                var filter = new StringBuilder($"{Bracket(deleted)} = 0");
                foreach (var property in index.Properties.Select(name => entityType.FindProperty(name)!).Where(static p => p.IsNullable))
                {
                    filter.Append(" AND ").Append(Bracket(property.GetColumnName())).Append(" IS NOT NULL");
                }

                return filter.ToString();
            default:
                return null;
        }
    }

    /// <summary>
    /// 标准 SQL 的双引号标识符（PostgreSQL、SQLite）。
    /// </summary>
    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>
    /// SQL Server 的方括号标识符。
    /// </summary>
    private static string Bracket(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
