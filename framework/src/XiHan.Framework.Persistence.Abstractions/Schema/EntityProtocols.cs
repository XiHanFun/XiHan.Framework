// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 实体实现的领域协议（可组合的标志）
/// </summary>
/// <remarks>
/// 见 RFC-0005「领域协议」：Provider 按这些协议维护审计、并发、软删除与租户字段
/// </remarks>
[Flags]
[ApiLevel(Stability.Preview, "5.0")]
public enum EntityProtocols
{
    /// <summary>
    /// 只有标识
    /// </summary>
    None = 0,

    /// <summary>
    /// 聚合根：带领域事件
    /// </summary>
    /// <remarks>
    /// 实现 <c>IAggregateRoot&lt;TKey&gt;</c>（即 <c>IHasDomainEvents</c>）
    /// </remarks>
    AggregateRoot = 1,

    /// <summary>
    /// 创建时间（<c>IHasCreationTime</c>）
    /// </summary>
    CreationTime = 2,

    /// <summary>
    /// 修改时间（<c>IHasModificationTime</c>）
    /// </summary>
    ModificationTime = 4,

    /// <summary>
    /// 并发戳（<c>IHasConcurrencyStamp</c>）
    /// </summary>
    ConcurrencyStamp = 8,

    /// <summary>
    /// 软删除（<c>ISoftDeletable</c>）
    /// </summary>
    SoftDeletable = 16,

    /// <summary>
    /// 多租户（<c>IMultiTenant</c>）
    /// </summary>
    MultiTenant = 32,
}
