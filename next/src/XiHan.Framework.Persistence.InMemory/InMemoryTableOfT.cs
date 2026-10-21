// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 一种实体的有界表。读取返回快照；写入由工作单元在提交时原子应用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryTable<TEntity, TKey>(int capacity)
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly Dictionary<TKey, TEntity> _rows = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// 容量上限。
    /// </summary>
    public int Capacity { get; } = capacity;

    /// <summary>
    /// 当前条数（含已软删除）。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _rows.Count;
            }
        }
    }

    /// <summary>
    /// 按标识读取。
    /// </summary>
    public Maybe<TEntity> Find(TKey id)
    {
        lock (_gate)
        {
            return _rows.TryGetValue(id, out var entity) ? Maybe.Some(entity) : default;
        }
    }

    /// <summary>
    /// 全表快照。
    /// </summary>
    public IReadOnlyList<TEntity> Snapshot()
    {
        lock (_gate)
        {
            return [.. _rows.Values];
        }
    }

    /// <summary>
    /// 原子应用一批变更。新增超出容量返回 <see cref="PersistenceErrors.CapacityExceeded"/>，新增撞键返回 <see cref="PersistenceErrors.Duplicate"/>；任一失败则整批不写。
    /// </summary>
    public Result Apply(IReadOnlyDictionary<TKey, TEntity> upserts, IReadOnlySet<TKey> removals)
    {
        ArgumentNullException.ThrowIfNull(upserts);
        ArgumentNullException.ThrowIfNull(removals);
        lock (_gate)
        {
            var additions = upserts.Keys.Count(key => !_rows.ContainsKey(key));
            if (_rows.Count - removals.Count(_rows.ContainsKey) + additions > Capacity)
            {
                return Error.Unavailable(PersistenceErrors.CapacityExceeded, $"{typeof(TEntity).Name} 的内存表已达容量上限 {Capacity}。");
            }

            foreach (var key in removals)
            {
                _rows.Remove(key);
            }

            foreach (var (key, entity) in upserts)
            {
                _rows[key] = entity;
            }

            return Result.Success();
        }
    }
}
