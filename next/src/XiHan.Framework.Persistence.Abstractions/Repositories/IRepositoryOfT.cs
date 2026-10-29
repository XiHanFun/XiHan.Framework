// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Repositories;

/// <summary>
/// 仓储。
/// </summary>
/// <remarks>
/// 聚合的集合视图。实现按实体实现的协议维护创建时间、修改时间、租户、并发戳与软删除。可恢复失败以 <see cref="Result"/> 返回。在 Transaction 阶段之外使用（没有活动的工作单元）是装配错误，每个操作都抛出携带 <see cref="PersistenceErrors.NoActiveUnitOfWork"/> 的 <see cref="FrameworkException"/>。批量方法按 <c>spec/conventions/batch-first.md</c>：单条是批量的特例，逐项结果与入参顺序一一对应，通过校验的项一次写入，批内重复项按入参顺序逐项施加语义，空集合不触后端但仍要求活动的工作单元。
/// </remarks>
/// <typeparam name="TEntity">实体类型。</typeparam>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IRepository<TEntity, in TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 按标识查找。
    /// </summary>
    /// <remarks>
    /// 已软删除或不属于当前租户的实体视为不存在。
    /// </remarks>
    ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken);

    /// <summary>
    /// 按标识集合查找，一次往返。
    /// </summary>
    /// <remarks>
    /// 只返回存在且可见的实体，顺序按入参，缺失省略，重复标识只返回一次。
    /// </remarks>
    Task<IReadOnlyList<TEntity>> FindManyAsync(IReadOnlyList<TKey> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 查询。
    /// </summary>
    Task<IReadOnlyList<TEntity>> ListAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken);

    /// <summary>
    /// 计数。
    /// </summary>
    /// <remarks>
    /// 忽略查询的排序与分页。
    /// </remarks>
    Task<long> CountAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken);

    /// <summary>
    /// 新增。
    /// </summary>
    /// <remarks>
    /// 标识重复返回 <see cref="PersistenceErrors.Duplicate"/>；多租户实体没有当前租户返回 <see cref="PersistenceErrors.TenantRequired"/>。
    /// </remarks>
    Task<Result> AddAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// 批量新增。
    /// </summary>
    /// <remarks>
    /// 逐项同 <see cref="AddAsync"/>，通过校验的项一次写入。结果与入参顺序一一对应；批内同一标识第二次返回 <see cref="PersistenceErrors.Duplicate"/>；写入阶段的失败对本次写入的全部项统一报告。
    /// </remarks>
    Task<IReadOnlyList<Result>> AddRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken);

    /// <summary>
    /// 更新。
    /// </summary>
    /// <remarks>
    /// 实体不存在返回 <see cref="PersistenceErrors.NotFound"/>；游离实例的并发戳与当前值不同返回 <see cref="PersistenceErrors.Concurrency"/>。
    /// </remarks>
    Task<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// 批量更新。
    /// </summary>
    /// <remarks>
    /// 逐项同 <see cref="UpdateAsync"/>，通过校验的项一次写入。结果与入参顺序一一对应；批内同一标识的游离实例第二次会因并发戳已刷新而返回 <see cref="PersistenceErrors.Concurrency"/>。
    /// </remarks>
    Task<IReadOnlyList<Result>> UpdateRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken);

    /// <summary>
    /// 删除。
    /// </summary>
    /// <remarks>
    /// 实现 <see cref="ISoftDeletable"/> 的实体改为打标。实体不存在返回 <see cref="PersistenceErrors.NotFound"/>。
    /// </remarks>
    Task<Result> RemoveAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// 批量删除。
    /// </summary>
    /// <remarks>
    /// 逐项同 <see cref="RemoveAsync"/>，通过校验的项一次写入。结果与入参顺序一一对应；批内同一实体第二次返回 <see cref="PersistenceErrors.NotFound"/>。
    /// </remarks>
    Task<IReadOnlyList<Result>> RemoveRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken);
}
