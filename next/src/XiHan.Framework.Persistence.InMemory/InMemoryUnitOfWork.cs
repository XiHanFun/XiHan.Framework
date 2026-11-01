// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存工作单元。
/// </summary>
/// <remarks>
/// 变更先进覆盖层（读己之写），提交时按涉及的表依次加锁，逐表校验每个写过的标识首次读到的状态（新增的标识已被别人提交即标识重复，更新或删除的行已被删、带戳的行戳已变即并发冲突），全部通过才逐表应用，跨表原子；回滚丢弃覆盖层。只读工作单元里仓储在写入时就拒绝（<see cref="PersistenceErrors.ReadOnly"/>），覆盖层始终为空；拒绝过写入的工作单元提交时回滚并返回同一错误。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryUnitOfWork(InMemoryStore store) : IUnitOfWork
{
    /// <summary>
    /// 提交目标存储。
    /// </summary>
    private readonly InMemoryStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// 按实体类型索引的覆盖层。
    /// </summary>
    /// <remarks>
    /// 工作单元是作用域服务，不需要并发保护。
    /// </remarks>
    private readonly Dictionary<Type, IPendingChanges> _pending = [];

    /// <summary>
    /// 写入阶段记下的失败。
    /// </summary>
    /// <remarks>
    /// 非空时提交只回滚并返回它。
    /// </remarks>
    private Error? _failure;

    /// <inheritdoc />
    public UnitOfWorkState State { get; private set; } = UnitOfWorkState.NotStarted;

    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public bool IsReadOnly { get; private set; }

    /// <inheritdoc />
    public DomainEventTracker DomainEvents { get; } = new();

    /// <inheritdoc />
    public Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (State != UnitOfWorkState.NotStarted)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The unit of work is in state '{State}' and cannot be started again.");
        }

        // 不支持命名连接（内存实现没有连接）：明确拒绝，不把绑定连接的工作单元当成普通工作单元静默开在缺省连接上
        if (options.Connection is { } connection)
        {
            throw new FrameworkException(PersistenceErrors.NotSupported, $"The in-memory persistence provider does not support binding a unit of work to the named connection '{connection}'.");
        }

        TenantId = options.TenantId;
        IsReadOnly = options.IsReadOnly;
        State = UnitOfWorkState.Active;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 校验失败（标识重复、并发冲突、容量已满）时任何一张表都不改，工作单元记为回滚，失败作为结果返回。
    /// </remarks>
    public Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (State != UnitOfWorkState.Active)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The unit of work is in state '{State}' and cannot be committed.");
        }

        // 写入阶段已失败（只读工作单元拒绝了写入）：处理器忽略了那次失败也不能当作成功，回滚并返回同一错误
        if (_failure is { } failure)
        {
            _pending.Clear();
            State = UnitOfWorkState.RolledBack;
            return Task.FromResult<Result>(failure);
        }

        var committed = Commit();
        _pending.Clear();
        State = committed.IsSuccess ? UnitOfWorkState.Committed : UnitOfWorkState.RolledBack;
        return Task.FromResult(committed);
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
    /// 标为失败。
    /// </summary>
    /// <remarks>
    /// 仓储在只读工作单元里拒绝写入时调用；只记第一次。
    /// </remarks>
    internal void Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _failure ??= error;
    }

    /// <summary>
    /// 取实体的覆盖层。
    /// </summary>
    internal PendingChanges<TEntity, TKey> Changes<TEntity, TKey>()
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
    /// 把覆盖层校验后应用到存储。
    /// </summary>
    /// <remarks>
    /// 涉及的表按 <see cref="IInMemoryTable.Order"/> 依次加锁（各工作单元同一次序，不会互相等死），在全部锁内先逐表校验、再逐表应用，失败时一张表都不改。
    /// </remarks>
    private Result Commit()
    {
        var targets = _pending.Values
            .Where(static p => p.HasChanges)
            .Select(p => (Pending: p, Table: p.Table(_store)))
            .OrderBy(static t => t.Table.Order)
            .ToList();
        var entered = 0;
        try
        {
            foreach (var (_, table) in targets)
            {
                table.Gate.Enter();
                entered++;
            }

            foreach (var (pending, _) in targets)
            {
                var validated = pending.Validate(_store);
                if (validated.IsFailure)
                {
                    return validated;
                }
            }

            foreach (var (pending, _) in targets)
            {
                pending.Apply(_store);
            }

            return Result.Success();
        }
        finally
        {
            for (var index = entered - 1; index >= 0; index--)
            {
                targets[index].Table.Gate.Exit();
            }
        }
    }

    /// <summary>
    /// 覆盖层的非泛型视图。
    /// </summary>
    internal interface IPendingChanges
    {
        /// <summary>
        /// 是否有变更。
        /// </summary>
        bool HasChanges { get; }

        /// <summary>
        /// 提交的目标表。
        /// </summary>
        IInMemoryTable Table(InMemoryStore store);

        /// <summary>
        /// 校验能否应用。
        /// </summary>
        /// <remarks>
        /// 调用方持有目标表的锁。
        /// </remarks>
        Result Validate(InMemoryStore store);

        /// <summary>
        /// 应用到目标表。
        /// </summary>
        /// <remarks>
        /// 调用方持有目标表的锁，且刚校验通过。
        /// </remarks>
        void Apply(InMemoryStore store);
    }

    /// <summary>
    /// 一种实体在本工作单元内的覆盖层。
    /// </summary>
    /// <remarks>
    /// 存的是副本：仓储写入时复制调用方实例，读取时再复制一次交出去，覆盖层里的对象只在提交时成为表里的行。
    /// </remarks>
    internal sealed class PendingChanges<TEntity, TKey> : IPendingChanges
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        /// <summary>
        /// 待新增或更新的实体，按标识索引。
        /// </summary>
        /// <remarks>
        /// 同一标识只保留最后一次写入。
        /// </remarks>
        private readonly Dictionary<TKey, TEntity> _upserts = [];

        /// <summary>
        /// 待物理删除的标识。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="_upserts"/> 互斥：同一标识不会同时出现在两边。
        /// </remarks>
        private readonly HashSet<TKey> _removals = [];

        /// <summary>
        /// 每个写过的标识首次写入时读到的已提交状态：当时是否有行、行上的并发戳。
        /// </summary>
        /// <remarks>
        /// 提交时据此校验别的工作单元在此期间有没有提交过同一标识。
        /// </remarks>
        private readonly Dictionary<TKey, (bool Existed, string? Stamp)> _bases = [];

        /// <inheritdoc />
        public bool HasChanges => _upserts.Count > 0 || _removals.Count > 0;

        /// <summary>
        /// 新增或更新。
        /// </summary>
        /// <param name="entity">覆盖层自己的副本。</param>
        /// <param name="committed">这次写入所依据的已提交行；标识已在覆盖层里时不用它。</param>
        public void Upsert(TEntity entity, TEntity? committed)
        {
            ArgumentNullException.ThrowIfNull(entity);
            Track(entity.Id, committed);
            // 同一工作单元内先删后加：撤销删除标记，让实体重新可见
            _removals.Remove(entity.Id);
            _upserts[entity.Id] = entity;
        }

        /// <summary>
        /// 物理删除。
        /// </summary>
        /// <param name="id">标识。</param>
        /// <param name="committed">这次删除所依据的已提交行；标识已在覆盖层里时不用它。</param>
        public void Remove(TKey id, TEntity? committed)
        {
            Track(id, committed);
            // 同一工作单元内先加后删：丢弃未提交的新增，并对表里可能存在的同键行记录删除
            _upserts.Remove(id);
            _removals.Add(id);
        }

        /// <summary>
        /// 在覆盖层里查：已删除返回标记。
        /// </summary>
        /// <remarks>
        /// 返回的是覆盖层自己的对象，调用方不得修改或交出。
        /// </remarks>
        public (bool Found, bool Removed, TEntity? Entity) Lookup(TKey id)
        {
            if (_removals.Contains(id))
            {
                return (true, true, null);
            }

            return _upserts.TryGetValue(id, out var entity) ? (true, false, entity) : (false, false, null);
        }

        /// <summary>
        /// 把覆盖层合并到表的行上。
        /// </summary>
        /// <remarks>
        /// 返回的是表与覆盖层自己的对象，调用方不得修改或交出。
        /// </remarks>
        public IEnumerable<TEntity> Overlay(IEnumerable<TEntity> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);
            // 表里被覆盖层删除或更新的行先跳过，再补上覆盖层的实体，保证每个标识只出现一次且以本工作单元的版本为准
            foreach (var entity in rows)
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
        public IInMemoryTable Table(InMemoryStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            return store.Table<TEntity, TKey>();
        }

        /// <inheritdoc />
        public Result Validate(InMemoryStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            return store.Table<TEntity, TKey>().Validate(_upserts, _removals, _bases);
        }

        /// <inheritdoc />
        public void Apply(InMemoryStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            store.Table<TEntity, TKey>().Apply(_upserts, _removals);
        }

        /// <summary>
        /// 首次写入一个标识时记下所依据的已提交状态。
        /// </summary>
        private void Track(TKey id, TEntity? committed)
            => _bases.TryAdd(id, committed is null ? (false, null) : (true, (committed as IHasConcurrencyStamp)?.ConcurrencyStamp));
    }
}
