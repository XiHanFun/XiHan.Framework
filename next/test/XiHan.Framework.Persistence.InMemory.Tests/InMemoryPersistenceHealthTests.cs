// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.InMemory.Tests;

/// <summary>
/// 健康项 <c>persistence.inmemory.capacity</c>。
/// </summary>
/// <remarks>
/// 宿主启动后 Ready，某种实体的表填满后 Degraded 并带 <see cref="PersistenceErrors.CapacityExceeded"/>。
/// </remarks>
public sealed class InMemoryPersistenceHealthTests
{
    /// <summary>
    /// 不实现任何协议的测试实体。
    /// </summary>
    private sealed class Widget : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }
    }

    [Fact]
    public async Task Health_IsReadyUntilATableIsFull_ThenDegraded()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>(),
            [new("XiHan:Persistence:InMemory:MaxEntitiesPerType", "1")],
            static services => services.AddRepository<Widget, Guid>());

        var ready = Assert.Single(host.App.Health().Contributions, static c => c.Name == InMemoryPersistenceFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, ready.State);

        var written = await host.InvokeAsync(async context =>
        {
            var repository = context.Services.GetRequiredService<IRepository<Widget, Guid>>();
            (await repository.AddAsync(new Widget { Id = Guid.NewGuid() }, context.CancellationToken)).ThrowIfFailure();
        });
        Assert.True(written.IsSuccess);

        var health = host.App.Health();
        var degraded = Assert.Single(health.Contributions, static c => c.Name == InMemoryPersistenceFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, degraded.State);
        Assert.Equal(PersistenceErrors.CapacityExceeded, degraded.Error?.Code);
        Assert.Equal(nameof(Widget), degraded.Error?.Details?["entity"]);
        Assert.Equal(1, degraded.Error?.Details?["capacity"]);
        Assert.True(health.Ready);
    }
}
