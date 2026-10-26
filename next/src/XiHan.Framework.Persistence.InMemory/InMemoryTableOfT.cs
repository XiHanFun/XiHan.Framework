// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 一种实体的有界表。读取返回快照；写入由工作单元在提交时原子应用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryTable<TEntity, TKey>(int capacity) : IInMemoryTable
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 已提交的行，按标识索引；所有访问都在 <see cref="_gate"/> 内进行。
    /// </summary>
    private readonly Dictionary<TKey, TEntity> _rows = [];

    /// <summary>
    /// 保护 <see cref="_rows"/> 的锁；读取只在锁内复制快照，写入在锁内整批应用。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 容量上限。
    /// </summary>
    public int Capacity { get; } = capacity;

    /// <inheritdoc />
    string IInMemoryTable.EntityName => typeof(TEntity).Name;

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
    /// 原子应用一批变更：同键覆盖、缺键新增、删除缺键忽略；重复标识的判定在仓储 <c>AddAsync</c> 完成，不在这里。新增超出容量返回 <see cref="PersistenceErrors.CapacityExceeded"/>，此时整批不写。
    /// </summary>
    public Result Apply(IReadOnlyDictionary<TKey, TEntity> upserts, IReadOnlySet<TKey> removals)
    {
        ArgumentNullException.ThrowIfNull(upserts);
        ArgumentNullException.ThrowIfNull(removals);
        lock (_gate)
        {
            // 先在锁内算出应用后的条数：现有条数减去确实存在的删除，加上真正新增的键（覆盖已有行不占容量）；
            // 校验通过前不改任何行，失败时表保持原样
            var additions = upserts.Keys.Count(key => !_rows.ContainsKey(key));
            if (_rows.Count - removals.Count(_rows.ContainsKey) + additions > Capacity)
            {
                return Error.Unavailable(PersistenceErrors.CapacityExceeded, PersistenceMessages.CapacityExceeded, MessageArguments.Of(("entity", typeof(TEntity).Name), ("capacity", Capacity)));
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
