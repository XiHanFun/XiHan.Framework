// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Repositories;

/// <summary>
/// 应用登记的实体集合。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class RepositoryRegistrations
{
    /// <summary>
    /// 按登记顺序保存的注册动作；每个动作把一种实体交给 Provider 注册。
    /// </summary>
    private readonly List<Action<IServiceCollection, IRepositoryRegistrar>> _registrations = [];

    /// <summary>
    /// <see cref="ApplyTo"/> 之后记住的服务集合；为 <see langword="null"/> 表示 Provider 尚未装配。
    /// </summary>
    private IServiceCollection? _services;

    /// <summary>
    /// <see cref="ApplyTo"/> 之后记住的 Provider 注册器；为 <see langword="null"/> 表示 Provider 尚未装配。
    /// </summary>
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
        // 先记住 Provider 再回放：此后 Add 看到非空的 _services 与 _registrar 就会直接注册，不会漏掉晚登记的实体
        _services = services;
        _registrar = registrar;
        foreach (var registration in _registrations)
        {
            registration(services, registrar);
        }
    }
}
