// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
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
    /// 按登记顺序保存的注册动作。
    /// </summary>
    /// <remarks>
    /// 每个动作把一种实体交给 Provider 注册。
    /// </remarks>
    private readonly List<Action<IServiceCollection, IRepositoryRegistrar>> _registrations = [];

    /// <summary>
    /// 登记过的实体，按首次登记的顺序，同一实体类型只一条。
    /// </summary>
    private readonly List<RegisteredEntity> _entities = [];

    /// <summary>
    /// <see cref="ApplyTo"/> 之后记住的服务集合。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示 Provider 尚未装配。
    /// </remarks>
    private IServiceCollection? _services;

    /// <summary>
    /// <see cref="ApplyTo"/> 之后记住的 Provider 注册器。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示 Provider 尚未装配。
    /// </remarks>
    private IRepositoryRegistrar? _registrar;

    /// <summary>
    /// 已登记的注册动作。
    /// </summary>
    public IReadOnlyList<Action<IServiceCollection, IRepositoryRegistrar>> Items => _registrations;

    /// <summary>
    /// 登记过的实体与标识类型。
    /// </summary>
    /// <remarks>
    /// 按首次登记的顺序，同一实体类型登记多次只一条。实体目录（<see cref="Schema.IEntityCatalog"/>）据此枚举。
    /// </remarks>
    public IReadOnlyList<RegisteredEntity> Entities => _entities;

    /// <summary>
    /// 实体登记时指定的命名连接。
    /// </summary>
    /// <remarks>
    /// 没有登记或登记时没有指定为 <see langword="null"/>。Provider 的落点规则据此决定实体落在哪个连接。
    /// </remarks>
    public string? ConnectionOf(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _entities.Find(e => e.EntityType == entityType)?.Connection;
    }

    /// <summary>
    /// 登记实体。
    /// </summary>
    /// <remarks>
    /// 同一实体可以登记多次（只记一条），但落点必须相同。
    /// </remarks>
    /// <param name="connection">实体落点的命名连接，见 <see cref="RegisteredEntity.Connection"/>；<see langword="null"/> 是 Provider 的缺省落点。</param>
    /// <exception cref="ArgumentException"><paramref name="connection"/> 为空白，或实体已登记到另一个连接（含一次为空、一次非空）。</exception>
    public void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity, TKey>(string? connection = null)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        var entity = new RegisteredEntity(typeof(TEntity), typeof(TKey), connection);
        var existing = _entities.Find(static e => e.EntityType == typeof(TEntity));
        if (existing is null)
        {
            _entities.Add(entity);
        }
        else if (!string.Equals(existing.Connection, connection, StringComparison.Ordinal))
        {
            // 落点只能有一个：两个特性各说各的连接时，静默取其一会让一方的数据落到它不知道的库里
            throw new ArgumentException($"Entity '{typeof(TEntity).FullName}' is already registered on connection '{existing.Connection ?? "(default)"}' and cannot also be registered on '{connection ?? "(default)"}'.", nameof(connection));
        }

        Action<IServiceCollection, IRepositoryRegistrar> registration = static (services, registrar) => registrar.Register<TEntity, TKey>(services);
        _registrations.Add(registration);
        if (_services is not null && _registrar is not null)
        {
            // Provider 已装配：立即注册，让后装配的特性也能登记实体
            registration(_services, _registrar);
        }
    }

    /// <summary>
    /// 让 Provider 完成全部注册。
    /// </summary>
    /// <remarks>
    /// 此后再登记的实体立即注册。
    /// </remarks>
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
