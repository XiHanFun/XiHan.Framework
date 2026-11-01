// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 写入失败后核对是否撞上了实体的唯一索引。
/// </summary>
/// <remarks>
/// 唯一索引取自实体信息（<c>[EntityIndex(IsUnique = true)]</c> 经约定转成的，以及实体自己的唯一 <c>[SugarIndex]</c>）。两种冲突：同一次写入里两项的键相同；库里已提交的另一行（标识不同）键相同。只在写入已经失败的路径上运行，不在正常写入前多查一次。
/// </remarks>
internal static class UniqueKeyConflicts
{
    /// <summary>
    /// 一次查询带的写入项上限。
    /// </summary>
    /// <remarks>
    /// 每列一个 <c>IN</c>，项多时分批，避免一条语句的参数过多（SQL Server 上限 2100）。
    /// </remarks>
    private const int ChunkSize = 200;

    /// <summary>
    /// 按入参顺序第一个撞上的唯一索引名。
    /// </summary>
    /// <remarks>
    /// 没有撞上返回 <see langword="null"/>。
    /// </remarks>
    /// <param name="client">写入所用的连接：取实体信息。</param>
    /// <param name="fresh">不在事务里的连接：读已提交的行（PostgreSQL 系语句失败后原事务已中止，不能再查）。</param>
    /// <param name="written">这次写入的实体。</param>
    public static async Task<string?> FindAsync<TEntity, TKey>(ISqlSugarClient client, ISqlSugarClient fresh, IReadOnlyList<TEntity> written)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        var indexes = client.EntityMaintenance.GetEntityInfo<TEntity>().Indexs;
        if (indexes is null)
        {
            return null;
        }

        foreach (var index in indexes.Where(static i => i.IsUnique))
        {
            var properties = index.IndexFields.Keys.Select(static name => typeof(TEntity).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)).OfType<PropertyInfo>().ToList();
            // 列不是公开属性（只按列名声明的 [SugarIndex]）时取不到值，这个索引核对不了
            if (properties.Count != index.IndexFields.Count)
            {
                continue;
            }

            if (Collides<TEntity, TKey>(written, properties) || await CollidesWithStoredAsync<TEntity, TKey>(fresh, written, properties).ConfigureAwait(false))
            {
                return index.IndexName;
            }
        }

        return null;
    }

    /// <summary>
    /// 同一次写入里是否有两项的键相同。
    /// </summary>
    /// <remarks>
    /// 含空值的键不参与：PostgreSQL、SQLite、MySQL 的唯一索引不约束含 NULL 的行。
    /// </remarks>
    private static bool Collides<TEntity, TKey>(IReadOnlyList<TEntity> written, List<PropertyInfo> properties)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        // 键 → 第一个持有它的标识；同一标识出现两次（同一实体在批里重复）不算冲突
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
    /// 库里是否有标识不同、键相同的已提交行。
    /// </summary>
    /// <remarks>
    /// 每列按这批写入项的取值做 <c>IN</c> 取回候选行（笛卡尔积的超集），再在内存里逐列比较；含空值的键不比较——PostgreSQL、SQLite、MySQL 的唯一索引不约束含 NULL 的行。字符串按序数比较：大小写不敏感排序规则的库上只差大小写的冲突核对不到，异常原样抛出。
    /// </remarks>
    private static async Task<bool> CollidesWithStoredAsync<TEntity, TKey>(ISqlSugarClient fresh, IReadOnlyList<TEntity> written, List<PropertyInfo> properties)
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
                // 每列一个 List<列类型>.Contains：SqlSugar 译成 IN
                var values = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(property.PropertyType))!;
                foreach (var value in chunk.Select(entity => property.GetValue(entity)).Distinct())
                {
                    values.Add(value);
                }

                var contains = Expression.Call(Expression.Constant(values), values.GetType().GetMethod(nameof(List<object>.Contains))!, Expression.Property(parameter, property));
                conditions.Add(contains);
            }

            var rows = await fresh.Queryable<TEntity>().Where(Expression.Lambda<Func<TEntity, bool>>(conditions.Aggregate(Expression.AndAlso), parameter)).ToListAsync().ConfigureAwait(false);
            if (chunk.Any(entity => rows.Exists(row => !row.Id.Equals(entity.Id) && SameKey(entity, row, properties))))
            {
                return true;
            }
        }

        return false;
    }

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
