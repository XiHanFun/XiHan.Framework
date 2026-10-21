// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// Provider 注册闭合泛型仓储的入口。应用通过 <see cref="RepositoryServiceCollectionExtensions.AddRepository{TEntity, TKey}"/> 登记实体，
/// Provider 的特性在配置阶段逐一调用本接口，因此换 Provider 不改业务代码，也不需要开放泛型或反射。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IRepositoryRegistrar
{
    /// <summary>
    /// 为实体注册 <see cref="IRepository{TEntity, TKey}"/>。
    /// </summary>
    void Register<TEntity, TKey>(IServiceCollection services)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>;
}

/// <summary>
/// 应用登记的实体集合。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class RepositoryRegistrations
{
    private readonly List<Action<IServiceCollection, IRepositoryRegistrar>> _registrations = [];

    /// <summary>
    /// 已登记的注册动作。
    /// </summary>
    public IReadOnlyList<Action<IServiceCollection, IRepositoryRegistrar>> Items => _registrations;

    /// <summary>
    /// 登记实体。
    /// </summary>
    public void Add<TEntity, TKey>()
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
        => _registrations.Add(static (services, registrar) => registrar.Register<TEntity, TKey>(services));

    /// <summary>
    /// 让 Provider 完成全部注册。
    /// </summary>
    public void ApplyTo(IServiceCollection services, IRepositoryRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registrar);
        foreach (var registration in _registrations)
        {
            registration(services, registrar);
        }
    }
}

/// <summary>
/// 实体登记扩展。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// 登记实体，由当前装配的持久化 Provider 注册仓储。必须在 <c>AddXiHan()</c> 之前调用。
    /// </summary>
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
