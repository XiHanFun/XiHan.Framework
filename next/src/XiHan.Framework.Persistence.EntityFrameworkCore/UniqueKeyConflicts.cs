// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 保存失败后核对是否撞上了实体的唯一索引。
/// </summary>
/// <remarks>
/// 唯一索引取自模型（<c>[EntityIndex(IsUnique = true)]</c> 经约定映射的，以及应用自己 <c>HasIndex(…).IsUnique()</c> 的）。两种冲突：同一次保存里两项的键相同；库里的另一行（标识不同）键相同。软删除实体的部分唯一索引（约定加了 <c>HasFilter</c> 的）只约束未删除的行：已删除的写入项与已删除的行都不参与比较；应用自己 <c>HasFilter</c> 的条件是任意 SQL，在内存里求不了值，照没有条件比较。SaveChanges 失败后已回到它自动建的保存点，查询在同一事务里进行，本工作单元先前写入的行也看得见。只在保存已经失败的路径上运行。
/// </remarks>
internal static class UniqueKeyConflicts
{
    /// <summary>
    /// 一次查询带的写入项上限。
    /// </summary>
    /// <remarks>
    /// 每列一个 <c>IN</c>，项多时分批，避免一条语句的参数过多。
    /// </remarks>
    private const int ChunkSize = 200;

    /// <summary>
    /// 按入参顺序第一个撞上的唯一索引名。
    /// </summary>
    /// <remarks>
    /// 没有撞上返回 <see langword="null"/>。
    /// </remarks>
    public static async Task<string?> FindAsync<TEntity, TKey>(DbContext context, IReadOnlyList<TEntity> written, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        if (context.Model.FindEntityType(typeof(TEntity)) is not { } entityType)
        {
            return null;
        }

        // 约定建的部分唯一索引按名字认出：模型里只有条件 SQL，换回声明才知道条件是“未删除”
        var partial = typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity))
            ? EntityIndexes.Resolve(typeof(TEntity), entityType.GetTableName() ?? typeof(TEntity).Name).Where(static i => i.ExcludesDeleted).Select(static i => i.Name).ToHashSet(StringComparer.Ordinal)
            : [];
        foreach (var index in entityType.GetIndexes().Where(static i => i.IsUnique))
        {
            var properties = index.Properties.Select(static p => p.PropertyInfo).OfType<PropertyInfo>().ToList();
            // 影子属性没有 CLR 属性可取值，这个索引核对不了
            if (properties.Count != index.Properties.Count)
            {
                continue;
            }

            // 部分唯一索引只约束未删除的行：已删除的写入项不会撞上它
            var excludesDeleted = index.GetDatabaseName() is { } name && partial.Contains(name);
            var constrained = excludesDeleted ? [.. written.Where(static e => !IsDeleted(e))] : written;
            if (Collides<TEntity, TKey>(constrained, properties) || await CollidesWithStoredAsync<TEntity, TKey>(context, constrained, properties, excludesDeleted, cancellationToken).ConfigureAwait(false))
            {
                return index.GetDatabaseName() ?? index.Name ?? string.Join('_', index.Properties.Select(static p => p.Name));
            }
        }

        return null;
    }

    /// <summary>
    /// 同一次保存里是否有两项的键相同。
    /// </summary>
    /// <remarks>
    /// 含空值的键不参与：PostgreSQL、SQLite、MySQL 的唯一索引不约束含 NULL 的行。
    /// </remarks>
    private static bool Collides<TEntity, TKey>(IReadOnlyList<TEntity> written, List<PropertyInfo> properties)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        // 键 → 第一个持有它的标识；同一标识出现两次不算冲突
        var seen = new Dictionary<object?[], TKey>(KeyComparer.Instance);
        foreach (var entity in written)
        {
            var key = properties.Select(p => p.GetValue(entity)).ToArray();
            if (Array.TrueForAll(key, static v => v is not null) && !seen.TryAdd(key, entity.Id) && !seen[key].Equals(entity.Id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 库里是否有标识不同、键相同的行。
    /// </summary>
    /// <remarks>
    /// 每列按这批写入项的取值做 <c>IN</c> 取回候选行（笛卡尔积的超集，不跟踪、不经全局过滤器），再在内存里逐列比较；含空值的键不比较。<paramref name="excludesDeleted"/> 为真（部分唯一索引）时已删除的行不比较。字符串按序数比较：大小写不敏感排序规则的库上只差大小写的冲突核对不到，异常原样抛出。
    /// </remarks>
    private static async Task<bool> CollidesWithStoredAsync<TEntity, TKey>(DbContext context, IReadOnlyList<TEntity> written, List<PropertyInfo> properties, bool excludesDeleted, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        var candidates = written.Where(entity => properties.TrueForAll(p => p.GetValue(entity) is not null)).ToList();
        foreach (var chunk in candidates.Chunk(ChunkSize))
        {
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var conditions = new List<Expression>(properties.Count);
            foreach (var property in properties)
            {
                // 每列一个 List<列类型>.Contains：EF Core 译成 IN
                var values = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(property.PropertyType))!;
                foreach (var value in chunk.Select(entity => property.GetValue(entity)).Distinct())
                {
                    values.Add(value);
                }

                var contains = Expression.Call(Expression.Constant(values), values.GetType().GetMethod(nameof(List<object>.Contains))!, Expression.Property(parameter, property));
                conditions.Add(contains);
            }

            var rows = await context.Set<TEntity>().AsNoTracking().IgnoreQueryFilters().Where(Expression.Lambda<Func<TEntity, bool>>(conditions.Aggregate(Expression.AndAlso), parameter)).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (chunk.Any(entity => rows.Exists(row => !row.Id.Equals(entity.Id) && !(excludesDeleted && IsDeleted(row)) && SameKey(entity, row, properties))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 实体是否已软删除。
    /// </summary>
    /// <remarks>
    /// 只对软删除实体调用（部分唯一索引只出现在软删除实体上）。
    /// </remarks>
    private static bool IsDeleted<TEntity>(TEntity entity)
        where TEntity : class
        => entity is ISoftDeletable { IsDeleted: true };

    /// <summary>
    /// 两个实体在这些列上的值是否都相等。
    /// </summary>
    private static bool SameKey<TEntity>(TEntity left, TEntity right, List<PropertyInfo> properties)
        where TEntity : class
        => properties.TrueForAll(p => Equals(p.GetValue(left), p.GetValue(right)));

    /// <summary>
    /// 按元素逐个比较的键相等比较器。
    /// </summary>
    private sealed class KeyComparer : IEqualityComparer<object?[]>
    {
        /// <summary>
        /// 共用实例。
        /// </summary>
        public static readonly KeyComparer Instance = new();

        /// <inheritdoc />
        public bool Equals(object?[]? x, object?[]? y)
            => ReferenceEquals(x, y) || (x is not null && y is not null && x.Length == y.Length && x.Zip(y).All(static pair => object.Equals(pair.First, pair.Second)));

        /// <inheritdoc />
        public int GetHashCode(object?[] obj)
        {
            var hash = new HashCode();
            foreach (var value in obj)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }
}
