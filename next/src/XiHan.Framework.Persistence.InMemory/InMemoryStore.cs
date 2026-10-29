// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存存储。
/// </summary>
/// <remarks>
/// 每种实体一张有界的表。单例。同时是健康项 <see cref="InMemoryPersistenceFeature.HealthName"/> 的贡献者：任一张表满即 Degraded。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryStore(InMemoryPersistenceOptions options) : IHealthContributor
{
    /// <summary>
    /// 按实体类型索引的表。
    /// </summary>
    /// <remarks>
    /// 值是对应的 <see cref="InMemoryTable{TEntity, TKey}"/>，并发创建由字典保证只保留一张。
    /// </remarks>
    private readonly ConcurrentDictionary<Type, object> _tables = new();

    /// <summary>
    /// 选项。
    /// </summary>
    public InMemoryPersistenceOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 取实体的表。
    /// </summary>
    public InMemoryTable<TEntity, TKey> Table<TEntity, TKey>()
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
        => (InMemoryTable<TEntity, TKey>)_tables.GetOrAdd(typeof(TEntity), _ => new InMemoryTable<TEntity, TKey>(Options.MaxEntitiesPerType));

    /// <summary>
    /// 清空全部表。
    /// </summary>
    public void Clear() => _tables.Clear();

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 任一张表满即 Degraded：对该实体的新增会被 CapacityExceeded 拒绝；按实体名序数顺序取第一个，报告稳定
        var full = _tables.Values.OfType<IInMemoryTable>().Where(static table => table.Count >= table.Capacity).OrderBy(static table => table.EntityName, StringComparer.Ordinal).FirstOrDefault();
        return
        [
            full is null
                ? new ContributorStatus(InMemoryPersistenceFeature.HealthName, LifecycleState.Ready)
                : new ContributorStatus(InMemoryPersistenceFeature.HealthName, LifecycleState.Degraded, Error.Unavailable(PersistenceErrors.CapacityExceeded, PersistenceMessages.CapacityExceeded, MessageArguments.Of(("entity", full.EntityName), ("capacity", full.Capacity)))),
        ];
    }
}
