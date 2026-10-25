// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions.Repositories;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 实体登记与 Provider 注册器之间的回放、即时注册与单例共享。
/// </summary>
public sealed class RepositoryRegistrationTests
{
    /// <summary>
    /// 只记录被登记实体名称的注册器替身，不向容器写入任何服务。
    /// </summary>
    private sealed class RecordingRegistrar : IRepositoryRegistrar
    {
        /// <summary>
        /// 按注册顺序记录的实体描述，形如 <c>Widget&lt;Guid&gt;</c>。
        /// </summary>
        public List<string> Registered { get; } = [];

        /// <inheritdoc />
        public void Register<TEntity, TKey>(IServiceCollection services)
            where TEntity : class, IEntity<TKey>
            where TKey : notnull, IEquatable<TKey>
            => Registered.Add($"{typeof(TEntity).Name}<{typeof(TKey).Name}>");
    }

    [Fact]
    public void AddRepository_RecordsEntities_AndApplyToDelegatesToRegistrar()
    {
        var services = new ServiceCollection();
        services.AddRepository<Widget, Guid>().AddRepository<AuditedWidget, Guid>();
        var registrar = new RecordingRegistrar();

        services.GetRepositoryRegistrations().ApplyTo(services, registrar);

        Assert.Equal(["Widget<Guid>", "AuditedWidget<Guid>"], registrar.Registered);
    }

    [Fact]
    public void AddRepository_AfterApplyTo_RegistersImmediately()
    {
        var services = new ServiceCollection();
        var registrar = new RecordingRegistrar();
        services.GetRepositoryRegistrations().ApplyTo(services, registrar);

        services.AddRepository<Widget, Guid>();

        Assert.Equal(["Widget<Guid>"], registrar.Registered);
    }

    [Fact]
    public void GetRepositoryRegistrations_ReturnsSingleSharedInstance()
    {
        var services = new ServiceCollection();

        var first = services.GetRepositoryRegistrations();
        var second = services.GetRepositoryRegistrations();

        Assert.Same(first, second);
        Assert.Single(services, static d => d.ServiceType == typeof(RepositoryRegistrations));
        Assert.Same(first, services.BuildServiceProvider().GetRequiredService<RepositoryRegistrations>());
    }
}
