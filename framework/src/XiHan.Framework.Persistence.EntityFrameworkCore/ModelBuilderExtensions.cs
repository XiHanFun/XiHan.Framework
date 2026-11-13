// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 按实体协议配置模型
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ModelBuilderExtensions
{
    /// <summary>
    /// SQLite 的 EF Core Provider 名
    /// </summary>
    internal const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

    /// <summary>
    /// SQL Server 的 EF Core Provider 名
    /// </summary>
    internal const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>
    /// PostgreSQL（Npgsql）的 EF Core Provider 名
    /// </summary>
    internal const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>
    /// 多租户实体的全局查询过滤器名
    /// </summary>
    /// <remarks>
    /// 要在工作单元里有意跨租户查询时，<c>IgnoreQueryFilters([TenantQueryFilter])</c> 只忽略这一个（ADR-0224）
    /// </remarks>
    public const string TenantQueryFilter = "xihan:tenant";

    /// <summary>
    /// 对已注册的每个实体
    /// </summary>
    /// <remarks>
    /// <see cref="IHasConcurrencyStamp"/> 的并发戳设为并发令牌（长度 32）；<see cref="IMultiTenant"/> 的租户与 <see cref="ISoftDeletable"/> 的删除标记建索引；实体上的 <see cref="EntityIndexAttribute"/> 映射为 <c>HasIndex</c>（唯一性与索引名按 <see cref="EntityIndexes"/>，名字用此刻的表名推出，与 SqlSugar 建出的同名）。软删除实体的唯一索引只约束未删除的行（<see cref="EntityIndex.ExcludesDeleted"/>），另加 <c>HasFilter</c>：条件按 <paramref name="database"/> 的 Provider 写方言（Npgsql <c>"IsDeleted" = false</c>、SQLite <c>"IsDeleted" = 0</c>、SQL Server <c>[IsDeleted] = 0</c>，SQL Server 上键含可空列时另加 <c>IS NOT NULL</c>，与它对唯一索引的缺省一致），列名取此刻的映射；其余关系型 Provider 不支持部分唯一索引，建模时抛出；非关系型 Provider 本来不建索引，不加条件。Provider 是 SQLite 时，模型里全部实体（含从属类型与复杂类型）的 <see cref="DateTimeOffset"/> 与可空 <see cref="DateTimeOffset"/> 属性存成 UTC ticks 整数（SQLite 的 EF Core Provider 存成文本时翻译不了大小比较与排序），应用已配了值转换或存储类型的属性不动。在 <c>OnModelCreating</c> 里注册全部实体、配置完表名、列名与自己的值转换之后调用：<c>modelBuilder.ApplyPersistenceConventions(Database)</c>
    /// </remarks>
    /// <param name="modelBuilder">模型构建器</param>
    /// <param name="database">本 DbContext 的 <see cref="DbContext.Database"/>：据它的 Provider 写部分唯一索引的条件</param>
    /// <exception cref="InvalidOperationException">实体的索引声明不合法（见 <see cref="EntityIndexes"/>）</exception>
    /// <exception cref="FrameworkException">有软删除实体声明了唯一索引，而 Provider 不支持部分唯一索引（<see cref="PersistenceErrors.NotSupported"/>，一次列出全部）</exception>
    public static ModelBuilder ApplyPersistenceConventions(this ModelBuilder modelBuilder, DatabaseFacade database)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(database);
        Apply(modelBuilder, database.ProviderName, database.IsRelational());
        // 过滤器引用建模时的上下文：EF Core 在每次查询时把它换成执行查询的上下文，所以按各自的工作单元取租户
        FilterByTenant(modelBuilder, database.GetService<ICurrentDbContext>().Context);
        return modelBuilder;
    }

    /// <summary>
    /// 给多租户实体加按工作单元租户过滤的命名查询过滤器
    /// </summary>
    /// <remarks>
    /// 仓储本来就按租户过滤，但工作单元的 <c>Context</c> 与作用域里的 DbContext 是公开的，直接查询会读到别的租户的行（ADR-0224）。条件：上下文在 XiHan 工作单元里时只留工作单元租户的行，不在时不过滤。只加在继承体系的根上；实体已有匿名过滤器时抛出，EF Core 不能混用匿名与命名过滤器
    /// </remarks>
    private static void FilterByTenant(ModelBuilder modelBuilder, DbContext context)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;
            if (!typeof(IMultiTenant).IsAssignableFrom(clrType) || entityType.BaseType is not null || entityType.IsOwned() || entityType.HasSharedClrType)
            {
                continue;
            }

            if (entityType.GetDeclaredQueryFilters().Any(static filter => filter.IsAnonymous))
            {
                throw new InvalidOperationException($"Entity '{clrType.FullName}' has an anonymous query filter, but XiHan adds the named tenant filter '{TenantQueryFilter}' and EF Core cannot combine anonymous and named filters. Give the existing filter a name with HasQueryFilter(\"<name>\", ...).");
            }

            // e => !Applies(context) || EF.Property<string>(e, "TenantId") == TenantOf(context)
            var entity = Expression.Parameter(clrType, "e");
            var current = Expression.Constant(context, typeof(DbContext));
            var applies = Expression.Call(((Func<DbContext, bool>)TenantQueryFilters.Applies).Method, current);
            var tenant = Expression.Call(((Func<DbContext, string?>)TenantQueryFilters.TenantOf).Method, current);
            var column = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(string)], entity, Expression.Constant(nameof(IMultiTenant.TenantId)));
            var body = Expression.OrElse(Expression.Not(applies), Expression.Equal(column, tenant));
            modelBuilder.Entity(clrType).HasQueryFilter(TenantQueryFilter, Expression.Lambda(body, entity));
        }
    }

    /// <summary>
    /// 按 Provider 名施加约定
    /// </summary>
    /// <remarks>
    /// 公开入口与测试共用：测试据此覆盖没有引用的 Provider（Npgsql、SQL Server、不支持的）
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

            // SQLite 的 EF Core Provider 把 DateTimeOffset 存成文本，翻译不了大小比较与 ORDER BY（到期书签、按时间分页都要）：
            // 存成 UTC ticks 整数。从属类型与共享类型实体的列同样要转换，所以放在下面的跳过之前
            if (string.Equals(providerName, SqliteProvider, StringComparison.Ordinal))
            {
                StoreDateTimeOffsetsAsUtcTicks(entityType);
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
    /// 把时刻属性存成 UTC ticks 整数
    /// </summary>
    /// <remarks>
    /// 类型上声明的 <see cref="DateTimeOffset"/> 与可空 <see cref="DateTimeOffset"/> 属性，递归到复杂类型
    /// 应用已经配了值转换或存储类型的属性（<c>HasConversion</c>、<c>ConfigureConventions</c> 里的 <c>HaveConversion</c>）不动：显式配置优先于约定，已有库的存储格式也就不变
    /// </remarks>
    private static void StoreDateTimeOffsetsAsUtcTicks(IMutableTypeBase type)
    {
        foreach (var property in type.GetDeclaredProperties())
        {
            if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) != typeof(DateTimeOffset)
                || property.GetValueConverter() is not null
                || property.GetProviderClrType() is not null)
            {
                continue;
            }

            // 不可空类型的转换器同样作用于可空属性：EF Core 不把 null 交给转换器
            property.SetValueConverter(UtcTicksConverter.Instance);
        }

        foreach (var complex in type.GetDeclaredComplexProperties())
        {
            StoreDateTimeOffsetsAsUtcTicks(complex.ComplexType);
        }
    }

    /// <summary>
    /// 部分唯一索引的条件：只约束未删除的行
    /// </summary>
    /// <remarks>
    /// Provider 不支持时为 <see langword="null"/>。列名取此刻的映射（<c>HasColumnName</c> 与命名约定要在约定之前生效）
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
    /// 标准 SQL 的双引号标识符（PostgreSQL、SQLite）
    /// </summary>
    internal static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>
    /// SQL Server 的方括号标识符
    /// </summary>
    private static string Bracket(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
