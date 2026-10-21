// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存存储：每种实体一张有界的表。单例。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryStore(InMemoryPersistenceOptions options)
{
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
}
