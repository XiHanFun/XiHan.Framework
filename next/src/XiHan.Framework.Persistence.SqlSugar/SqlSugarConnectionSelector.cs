// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Reflection;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 默认的实体落点规则，按顺序。
/// </summary>
/// <remarks>
/// 实体带 SqlSugar <see cref="TenantAttribute"/>（<c>[Tenant(configId)]</c>）→ 客户端里该 <c>ConfigId</c> 的连接；实体实现 <see cref="IMultiTenant"/> 且客户端有 <see cref="SqlSugarConnections.Shard"/> 连接 → 分片连接；其余 → 根（默认）连接。平台目录表因此始终落默认连接，租户表在带分片的调用里落分片连接。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SqlSugarConnectionSelector : IEntityConnectionSelector
{
    /// <summary>
    /// 实体类型 → <c>[Tenant]</c> 特性指定的 <c>ConfigId</c>（没有特性为 <see langword="null"/>）。
    /// </summary>
    /// <remarks>
    /// 特性查找只做一次。
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, string?> _pinned = new();

    /// <inheritdoc />
    public ISqlSugarClient ConnectionFor(Type entityType, ISqlSugarClient client)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(client);
        var tenant = client.AsTenant();

        // 显式标注优先：这是应用对落点的明确声明，指向的连接不存在就是装配错误，不回退到默认连接
        if (_pinned.GetOrAdd(entityType, static type => type.GetCustomAttribute<TenantAttribute>()?.configId?.ToString()) is { } pinned)
        {
            return tenant.IsAnyConnection(pinned)
                ? tenant.GetConnection(pinned)
                : throw new FrameworkException(PersistenceErrors.StoreUnavailable, $"Entity '{entityType.FullName}' is pinned to connection '{pinned}' by [Tenant], but the SqlSugar client has no connection with that ConfigId.");
        }

        // 租户数据在带分片的调用里才有 shard 连接；没有分片的调用（共库租户、无租户）与平台数据一样走根连接
        if (typeof(IMultiTenant).IsAssignableFrom(entityType) && tenant.IsAnyConnection(SqlSugarConnections.Shard))
        {
            return tenant.GetConnection(SqlSugarConnections.Shard);
        }

        return client;
    }
}
