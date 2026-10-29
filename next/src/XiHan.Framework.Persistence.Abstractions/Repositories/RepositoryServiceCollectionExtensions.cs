// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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
    /// 可在 <c>AddXiHan()</c> 之前调用，也可在特性的 <c>Configure</c> 里调用。
    /// </remarks>
    public static IServiceCollection AddRepository<TEntity, TKey>(this IServiceCollection services)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.GetRepositoryRegistrations().Add<TEntity, TKey>();
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
