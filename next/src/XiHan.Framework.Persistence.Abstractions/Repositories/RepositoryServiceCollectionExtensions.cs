// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Repositories;

/// <summary>
/// 实体登记扩展。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// 登记实体，由当前装配的持久化 Provider 注册仓储。
    /// </summary>
    /// <remarks>
    /// 可在 <c>AddXiHan()</c> 之前调用，也可在特性的 <c>Configure</c> 里调用。<paramref name="connection"/> 把实体登记到命名连接（RFC-0005「命名连接」）：SqlSugar 是客户端里同名 <c>ConfigId</c> 的连接，EF Core 与内存实现不支持，装配时抛出携带 <see cref="PersistenceErrors.NotSupported"/> 的异常。
    /// </remarks>
    /// <param name="services">服务集合。</param>
    /// <param name="connection">实体落点的命名连接；<see langword="null"/> 是 Provider 的缺省落点。</param>
    /// <exception cref="ArgumentException"><paramref name="connection"/> 为空白，或实体已登记到另一个连接。</exception>
    public static IServiceCollection AddRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity, TKey>(this IServiceCollection services, string? connection = null)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.GetRepositoryRegistrations().Add<TEntity, TKey>(connection);
        return services;
    }

    /// <summary>
    /// 取（必要时创建）登记集合。
    /// </summary>
    public static RepositoryRegistrations GetRepositoryRegistrations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 在服务描述里找已有实例而不是另建：应用、Provider 与多个特性必须共享同一个登记集合，
        // 否则 Provider 装配前登记的实体会丢失
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(RepositoryRegistrations) && descriptor.ImplementationInstance is RepositoryRegistrations existing)
            {
                return existing;
            }
        }

        var registrations = new RepositoryRegistrations();
        services.AddSingleton(registrations);
        return registrations;
    }
}
