// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 一种实体的有界表
/// </summary>
/// <remarks>
/// 表里存的是副本，公开的读取也只交出副本，调用方改动读出的实例碰不到表。写入由工作单元在提交时校验后原子应用：在锁内按每个写过的标识首次读到的状态校验，全部通过才改行
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryTable<TEntity, TKey>(int capacity) : IInMemoryTable
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 已提交的行，按标识索引
    /// </summary>
    /// <remarks>
    /// 所有访问都在 <see cref="_gate"/> 内进行。行对象进表后不再被修改（提交整行替换），表外拿到的只有副本
    /// </remarks>
    private readonly Dictionary<TKey, TEntity> _rows = [];

    /// <summary>
    /// 保护 <see cref="_rows"/> 的锁
    /// </summary>
    /// <remarks>
    /// 读取只在锁内取行，提交在锁内校验并应用
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>
    /// 容量上限
    /// </summary>
    public int Capacity { get; } = capacity;

    /// <inheritdoc />
    string IInMemoryTable.EntityName => typeof(TEntity).Name;

    /// <inheritdoc />
    long IInMemoryTable.Order => Order;

    /// <inheritdoc />
    Lock IInMemoryTable.Gate => _gate;

    /// <summary>
    /// 当前条数（含已软删除）
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
    /// 一次提交涉及多张表时的加锁次序
    /// </summary>
    /// <remarks>
    /// 由 <see cref="InMemoryStore"/> 建表时按建表先后分配，各工作单元按同一次序加锁，不会互相等死
    /// </remarks>
    internal long Order { get; init; }

    /// <summary>
    /// 按标识读取一个副本
    /// </summary>
    public Maybe<TEntity> Find(TKey id) => Row(id) is { } row ? Maybe.Some(EntityCopy.Of(row)) : default;

    /// <summary>
    /// 全表快照：每行一个副本
    /// </summary>
    public IReadOnlyList<TEntity> Snapshot() => [.. Rows().Select(EntityCopy.Of)];

    /// <summary>
    /// 按标识取表里的行本身
    /// </summary>
    /// <remarks>
    /// 只供仓储内部判定与过滤，不交给调用方，也不修改它
    /// </remarks>
    internal TEntity? Row(TKey id)
    {
        lock (_gate)
        {
            return _rows.GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// 全部行本身
    /// </summary>
    /// <remarks>
    /// 锁内复制出列表；只供仓储内部判定与过滤，不交给调用方，也不修改它们
    /// </remarks>
    internal IReadOnlyList<TEntity> Rows()
    {
        lock (_gate)
        {
            return [.. _rows.Values];
        }
    }

    /// <summary>
    /// 校验一批变更能否应用
    /// </summary>
    /// <remarks>
    /// 调用方持有 <see cref="IInMemoryTable.Gate"/>。<paramref name="bases"/> 是每个写过的标识首次读到的已提交状态：当时不存在、如今已有行 → <see cref="PersistenceErrors.Duplicate"/>（并发新增同一标识，后提交者失败）；当时存在、如今已不在 → <see cref="PersistenceErrors.Concurrency"/>；带并发戳的行戳已变 → <see cref="PersistenceErrors.Concurrency"/>。之后按应用后的条数校验容量（<see cref="PersistenceErrors.CapacityExceeded"/>）
    /// </remarks>
    internal Result Validate(IReadOnlyDictionary<TKey, TEntity> upserts, IReadOnlySet<TKey> removals, IReadOnlyDictionary<TKey, (bool Existed, string? Stamp)> bases)
    {
        foreach (var (key, basis) in bases)
        {
            var exists = _rows.TryGetValue(key, out var current);
            if (!basis.Existed)
            {
                if (exists)
                {
                    return Error.Conflict(PersistenceErrors.Duplicate, PersistenceMessages.Duplicate, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", key)));
                }

                continue;
            }

            if (!exists)
            {
                return Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.ConcurrencyConflict, MessageArguments.Of(("entity", typeof(TEntity).Name), ("reason", "the row was removed by another unit of work after it was read")));
            }

            if (basis.Stamp is not null && current is IHasConcurrencyStamp stamped && !string.Equals(stamped.ConcurrencyStamp, basis.Stamp, StringComparison.Ordinal))
            {
                return Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.StaleStamp, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", key)));
            }
        }

        // 应用后的条数：现有条数减去确实存在的删除，加上真正新增的键（覆盖已有行不占容量）
        var additions = upserts.Keys.Count(key => !_rows.ContainsKey(key));
        return _rows.Count - removals.Count(_rows.ContainsKey) + additions > Capacity
            ? Error.Unavailable(PersistenceErrors.CapacityExceeded, PersistenceMessages.CapacityExceeded, MessageArguments.Of(("entity", typeof(TEntity).Name), ("capacity", Capacity)))
            : Result.Success();
    }

    /// <summary>
    /// 应用已通过 <see cref="Validate"/> 的一批变更
    /// </summary>
    /// <remarks>
    /// 调用方持有 <see cref="IInMemoryTable.Gate"/>，且同一把锁下刚校验过。<paramref name="upserts"/> 里是覆盖层自己的副本，直接成为表里的行
    /// </remarks>
    internal void Apply(IReadOnlyDictionary<TKey, TEntity> upserts, IReadOnlySet<TKey> removals)
    {
        foreach (var key in removals)
        {
            _rows.Remove(key);
        }

        foreach (var (key, entity) in upserts)
        {
            _rows[key] = entity;
        }
    }
}
