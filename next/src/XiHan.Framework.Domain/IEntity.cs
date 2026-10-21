// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain;

/// <summary>
/// 有标识的实体。
/// </summary>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IEntity<out TKey>
    where TKey : notnull
{
    /// <summary>
    /// 标识。
    /// </summary>
    TKey Id { get; }
}

/// <summary>
/// 聚合根：一致性边界，领域事件的来源。
/// </summary>
/// <typeparam name="TKey">标识类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IAggregateRoot<out TKey> : IEntity<TKey>, IHasDomainEvents
    where TKey : notnull;

/// <summary>
/// 值对象标记。用 <c>record</c> 声明以获得值相等。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IValueObject;

/// <summary>
/// 软删除：持久化子系统把删除改为打标，并默认过滤已删除实体。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ISoftDeletable
{
    /// <summary>
    /// 是否已删除。
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间（UTC）。
    /// </summary>
    DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// 创建时间：持久化子系统在新增时写入。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasCreationTime
{
    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// 修改时间：持久化子系统在更新时写入。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasModificationTime
{
    /// <summary>
    /// 最近修改时间（UTC）。
    /// </summary>
    DateTimeOffset? ModifiedAt { get; set; }
}

/// <summary>
/// 多租户：持久化子系统在新增时写入当前租户，查询时按当前租户过滤。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMultiTenant
{
    /// <summary>
    /// 租户 ID。
    /// </summary>
    string TenantId { get; set; }
}

/// <summary>
/// 乐观并发戳：持久化子系统在新增与更新时刷新，更新时比对。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasConcurrencyStamp
{
    /// <summary>
    /// 并发戳。
    /// </summary>
    string ConcurrencyStamp { get; set; }
}
