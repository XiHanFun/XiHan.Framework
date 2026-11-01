// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Reflection;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 默认的实体落点规则，按顺序。
/// </summary>
/// <remarks>
/// 实体带 SqlSugar <see cref="TenantAttribute"/>（<c>[Tenant(configId)]</c>）→ 客户端里该 <c>ConfigId</c> 的连接；登记时指定了命名连接（<see cref="RepositoryRegistrations.ConnectionOf"/>）→ 同名 <c>ConfigId</c> 的连接；实体实现 <see cref="IMultiTenant"/> 且客户端有 <see cref="SqlSugarConnections.Shard"/> 连接 → 分片连接；其余 → 根（默认）连接。平台目录表因此始终落默认连接，租户表在带分片的调用里落分片连接。<c>[Tenant]</c> 与登记的连接都有而不同、或指向的连接不在客户端里，抛出携带 <see cref="PersistenceErrors.UnknownConnection"/> 的 <see cref="FrameworkException"/>，不回退到默认连接。
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

    /// <summary>
    /// 实体类型 → 生效的落点（<c>[Tenant]</c> 或登记的命名连接，都没有为 <see langword="null"/>）。
    /// </summary>
    /// <remarks>
    /// 登记集合在装配完成后不再变化，每种实体只解析一次。
    /// </remarks>
    private readonly ConcurrentDictionary<Type, string?> _placements = new();

    /// <summary>
    /// 应用登记的实体集合，提供登记时指定的命名连接。
    /// </summary>
    private readonly RepositoryRegistrations _registrations;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="registrations">应用登记的实体集合（容器里的单例）。</param>
    public SqlSugarConnectionSelector(RepositoryRegistrations registrations)
    {
        _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
    }

    /// <inheritdoc />
    public ISqlSugarClient ConnectionFor(Type entityType, ISqlSugarClient client)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(client);
        var tenant = client.AsTenant();

        // 显式落点优先：这是应用对落点的明确声明，指向的连接不存在就是装配错误，不回退到默认连接
        if (_placements.GetOrAdd(entityType, type => Placement(type, _registrations.ConnectionOf(type))) is { } configId)
        {
            return tenant.IsAnyConnection(configId) ? tenant.GetConnection(configId) : throw UnknownConnection(entityType, configId);
        }

        // 租户数据在带分片的调用里才有 shard 连接；没有分片的调用（共库租户、无租户）与平台数据一样走根连接
        if (typeof(IMultiTenant).IsAssignableFrom(entityType) && tenant.IsAnyConnection(SqlSugarConnections.Shard))
        {
            return tenant.GetConnection(SqlSugarConnections.Shard);
        }

        return client;
    }

    /// <summary>
    /// 实体的显式落点：<c>[Tenant(configId)]</c> 或登记时指定的命名连接。
    /// </summary>
    /// <remarks>
    /// 都没有为 <see langword="null"/>；两者都有而不同抛出 <see cref="PersistenceErrors.UnknownConnection"/>——代码里的声明与装配时的登记各说各的，静默取其一会让数据落到另一方不知道的库里。
    /// </remarks>
    /// <param name="entityType">实体类型。</param>
    /// <param name="registered">登记时指定的命名连接。</param>
    internal static string? Placement(Type entityType, string? registered)
    {
        var pinned = PinnedConnection(entityType);
        if (pinned is not null && registered is not null && !string.Equals(pinned, registered, StringComparison.Ordinal))
        {
            throw new FrameworkException(PersistenceErrors.UnknownConnection, $"Entity '{entityType.FullName}' is pinned to connection '{pinned}' by [Tenant] but registered on connection '{registered}'; keep one of them.");
        }

        return pinned ?? registered;
    }

    /// <summary>
    /// 实体的 <c>[Tenant(configId)]</c> 指定的连接。
    /// </summary>
    /// <remarks>
    /// 没有特性为 <see langword="null"/>。特性查找只做一次。
    /// </remarks>
    internal static string? PinnedConnection(Type entityType)
        => _pinned.GetOrAdd(entityType, static type => type.GetCustomAttribute<TenantAttribute>()?.configId?.ToString());

    /// <summary>
    /// 落点指向的连接不存在：装配错误。
    /// </summary>
    internal static FrameworkException UnknownConnection(Type entityType, string configId)
        => new(PersistenceErrors.UnknownConnection, $"Entity '{entityType.FullName}' is placed on connection '{configId}' (by [Tenant] or its registration), but the SqlSugar client has no connection with that ConfigId; add the connection or fix the placement.");
}
