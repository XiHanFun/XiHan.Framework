// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存工作单元：变更先进覆盖层（读己之写），提交时按表原子应用；回滚丢弃。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryUnitOfWork(InMemoryStore store) : IUnitOfWork
{
    /// <summary>
    /// 提交目标存储。
    /// </summary>
    private readonly InMemoryStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// 按实体类型索引的覆盖层；工作单元是作用域服务，不需要并发保护。
    /// </summary>
    private readonly Dictionary<Type, IPendingChanges> _pending = [];

    /// <inheritdoc />
    public UnitOfWorkState State { get; private set; } = UnitOfWorkState.NotStarted;

    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public bool IsReadOnly { get; private set; }

    /// <inheritdoc />
    public Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (State != UnitOfWorkState.NotStarted)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The unit of work is in state '{State}' and cannot be started again.");
        }

        TenantId = options.TenantId;
        IsReadOnly = options.IsReadOnly;
        State = UnitOfWorkState.Active;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (State != UnitOfWorkState.Active)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The unit of work is in state '{State}' and cannot be committed.");
        }

        // 只读工作单元允许仓储把变更写进覆盖层，但在提交点统一拒绝并回滚，而不是在每次写入时判断
        if (IsReadOnly && _pending.Values.Any(static p => p.HasChanges))
        {
            State = UnitOfWorkState.RolledBack;
            return Task.FromResult<Result>(Error.Validation(PersistenceErrors.InvalidUnitOfWorkState, PersistenceMessages.ReadOnly));
        }

        // 每张表各自原子应用；跨表不是一个事务，某张表失败时先前已应用的表不会撤销，工作单元整体记为回滚
        foreach (var pending in _pending.Values)
        {
            var applied = pending.Apply(_store);
            if (applied.IsFailure)
            {
                State = UnitOfWorkState.RolledBack;
                return Task.FromResult(applied);
            }
        }

        _pending.Clear();
        State = UnitOfWorkState.Committed;
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        // 回滚只需丢弃覆盖层；表从未被未提交的变更触碰过
        _pending.Clear();
        State = UnitOfWorkState.RolledBack;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 取实体的覆盖层。
    /// </summary>
    public PendingChanges<TEntity, TKey> Changes<TEntity, TKey>()
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        if (!_pending.TryGetValue(typeof(TEntity), out var pending))
        {
            pending = new PendingChanges<TEntity, TKey>();
            _pending[typeof(TEntity)] = pending;
        }

        return (PendingChanges<TEntity, TKey>)pending;
    }

    /// <summary>
    /// 覆盖层的非泛型视图。
    /// </summary>
    [ApiLevel(Stability.Preview, "5.0")]
    public interface IPendingChanges
    {
        /// <summary>
        /// 是否有变更。
        /// </summary>
        bool HasChanges { get; }

        /// <summary>
        /// 应用到存储。
        /// </summary>
        Result Apply(InMemoryStore store);
    }

    /// <summary>
    /// 一种实体在本工作单元内的覆盖层。
    /// </summary>
    [ApiLevel(Stability.Preview, "5.0")]
    public sealed class PendingChanges<TEntity, TKey> : IPendingChanges
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        /// <summary>
        /// 待新增或更新的实体，按标识索引；同一标识只保留最后一次写入。
        /// </summary>
        private readonly Dictionary<TKey, TEntity> _upserts = [];

        /// <summary>
        /// 待物理删除的标识。与 <see cref="_upserts"/> 互斥：同一标识不会同时出现在两边。
        /// </summary>
        private readonly HashSet<TKey> _removals = [];

        /// <inheritdoc />
        public bool HasChanges => _upserts.Count > 0 || _removals.Count > 0;

        /// <summary>
        /// 新增或更新。
        /// </summary>
        public void Upsert(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            // 同一工作单元内先删后加：撤销删除标记，让实体重新可见
            _removals.Remove(entity.Id);
            _upserts[entity.Id] = entity;
        }

        /// <summary>
        /// 物理删除。
        /// </summary>
        public void Remove(TKey id)
        {
            // 同一工作单元内先加后删：丢弃未提交的新增，并对表里可能存在的同键行记录删除
            _upserts.Remove(id);
            _removals.Add(id);
        }

        /// <summary>
        /// 在覆盖层里查：已删除返回标记。
        /// </summary>
        public (bool Found, bool Removed, TEntity? Entity) Lookup(TKey id)
        {
            if (_removals.Contains(id))
            {
                return (true, true, null);
            }

            return _upserts.TryGetValue(id, out var entity) ? (true, false, entity) : (false, false, null);
        }

        /// <summary>
        /// 把覆盖层合并到表快照上。
        /// </summary>
        public IEnumerable<TEntity> Overlay(IEnumerable<TEntity> snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            // 快照里被覆盖层删除或更新的行先跳过，再补上覆盖层的实体，保证每个标识只出现一次且以本工作单元的版本为准
            foreach (var entity in snapshot)
            {
                if (_removals.Contains(entity.Id) || _upserts.ContainsKey(entity.Id))
                {
                    continue;
                }

                yield return entity;
            }

            foreach (var entity in _upserts.Values)
            {
                yield return entity;
            }
        }

        /// <inheritdoc />
        public Result Apply(InMemoryStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            return store.Table<TEntity, TKey>().Apply(_upserts, _removals);
        }
    }
}
