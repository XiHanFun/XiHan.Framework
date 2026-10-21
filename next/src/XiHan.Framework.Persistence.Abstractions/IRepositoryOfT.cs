// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

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
