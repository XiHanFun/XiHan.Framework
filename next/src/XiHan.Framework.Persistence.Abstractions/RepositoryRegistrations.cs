// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 应用登记的实体集合。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class RepositoryRegistrations
{
    private readonly List<Action<IServiceCollection, IRepositoryRegistrar>> _registrations = [];
    private IServiceCollection? _services;
    private IRepositoryRegistrar? _registrar;

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
    {
        Action<IServiceCollection, IRepositoryRegistrar> registration = static (services, registrar) => registrar.Register<TEntity, TKey>(services);
        _registrations.Add(registration);
        if (_services is not null && _registrar is not null)
        {
            // Provider 已装配：立即注册，让后装配的特性也能登记实体
            registration(_services, _registrar);
        }
    }

    /// <summary>
    /// 让 Provider 完成全部注册；此后再登记的实体立即注册。
    /// </summary>
    public void ApplyTo(IServiceCollection services, IRepositoryRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registrar);
        _services = services;
        _registrar = registrar;
        foreach (var registration in _registrations)
        {
            registration(services, registrar);
        }
    }
}
