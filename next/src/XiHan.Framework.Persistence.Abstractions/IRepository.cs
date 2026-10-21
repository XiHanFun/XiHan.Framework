// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 查询的一项排序。
/// </summary>
/// <typeparam name="T">实体类型。</typeparam>
/// <param name="KeySelector">排序键。</param>
/// <param name="Descending">是否降序。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record Ordering<T>(Expression<Func<T, object?>> KeySelector, bool Descending = false);

/// <summary>
/// <see cref="EntityQuery{T}"/> 的工厂与常量。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class EntityQuery
{
    /// <summary>
    /// 默认页大小上限。
    /// </summary>
    public const int DefaultMaxTake = 1_000;

    /// <summary>
    /// 无条件、默认分页。
    /// </summary>
    public static EntityQuery<T> All<T>() => AllOf<T>.Instance;

    private static class AllOf<T>
    {
        public static readonly EntityQuery<T> Instance = new();
    }
}

/// <summary>
/// 实体查询：过滤、排序、分页。不可变，用 <c>with</c> 或链式方法派生。
/// </summary>
/// <typeparam name="T">实体类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record EntityQuery<T>
{
    /// <summary>
    /// 过滤规约；<see langword="null"/> 为不过滤。
    /// </summary>
    public ISpecification<T>? Filter { get; init; }

    /// <summary>
    /// 排序，按顺序应用。
    /// </summary>
    public IReadOnlyList<Ordering<T>> Orderings { get; init; } = [];

    /// <summary>
    /// 跳过条数。
    /// </summary>
    public int Skip
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Skip 不能为负。");
    }

    /// <summary>
    /// 取出条数上限；Provider 不得返回超过此数的结果。
    /// </summary>
    public int Take
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Take 必须为正。");
    } = EntityQuery.DefaultMaxTake;

    /// <summary>
    /// 是否包含已软删除的实体。
    /// </summary>
    public bool IncludeDeleted { get; init; }

    /// <summary>
    /// 派生带过滤的查询。
    /// </summary>
    public EntityQuery<T> Where(ISpecification<T> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return this with { Filter = Filter is null ? filter : new Specification<T>(Filter.Criteria).And(filter) };
    }

    /// <summary>
    /// 派生追加排序的查询。
    /// </summary>
    public EntityQuery<T> OrderBy(Expression<Func<T, object?>> keySelector, bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return this with { Orderings = [.. Orderings, new Ordering<T>(keySelector, descending)] };
    }

    /// <summary>
    /// 派生分页的查询。
    /// </summary>
    public EntityQuery<T> Page(int skip, int take) => this with { Skip = skip, Take = take };
}

/// <summary>
/// 仓储：聚合的集合视图。实现按实体实现的协议维护创建时间、修改时间、租户、并发戳与软删除。
/// 可恢复失败以 <see cref="Result"/> 返回。在 Transaction 阶段之外使用（没有活动的工作单元）是装配错误，
/// 每个操作都抛出携带 <see cref="PersistenceErrors.NoActiveUnitOfWork"/> 的 <see cref="FrameworkException"/>。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IRepository<TEntity, in TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 按标识查找；已软删除或不属于当前租户的实体视为不存在。
    /// </summary>
    ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken);

    /// <summary>
    /// 查询。
    /// </summary>
    Task<IReadOnlyList<TEntity>> ListAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken);

    /// <summary>
    /// 计数；忽略查询的排序与分页。
    /// </summary>
    Task<long> CountAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken);

    /// <summary>
    /// 新增。标识重复返回 <see cref="PersistenceErrors.Duplicate"/>；多租户实体没有当前租户返回 <see cref="PersistenceErrors.TenantRequired"/>。
    /// </summary>
    Task<Result> AddAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// 更新。实体不存在返回 <see cref="PersistenceErrors.NotFound"/>。
    /// </summary>
    Task<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// 删除；实现 <see cref="ISoftDeletable"/> 的实体改为打标。实体不存在返回 <see cref="PersistenceErrors.NotFound"/>。
    /// </summary>
    Task<Result> RemoveAsync(TEntity entity, CancellationToken cancellationToken);
}

/// <summary>
/// 标识生成器。
/// </summary>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IIdGenerator<out TKey>
    where TKey : notnull
{
    /// <summary>
    /// 生成一个新标识。
    /// </summary>
    TKey NewId();
}

/// <summary>
/// 基于时间有序的 UUID v7 标识生成器。
/// </summary>
/// <param name="timeProvider">时间源。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class GuidV7IdGenerator(TimeProvider timeProvider) : IIdGenerator<Guid>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public Guid NewId() => Guid.CreateVersion7(_timeProvider.GetUtcNow());
}
