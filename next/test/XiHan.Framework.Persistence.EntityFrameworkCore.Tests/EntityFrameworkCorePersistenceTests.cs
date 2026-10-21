// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Conformance;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

public sealed class EntityFrameworkCorePersistenceTests : IDisposable
{
    private readonly SqliteDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        Assert.False(EntityFrameworkCorePersistenceOptions.FromConfiguration(Configuration()).EnsureCreated);
        Assert.True(EntityFrameworkCorePersistenceOptions.FromConfiguration(Configuration(("XiHan:Persistence:EntityFrameworkCore:EnsureCreated", "true"))).EnsureCreated);
        Assert.Throws<ArgumentException>(static () => EntityFrameworkCorePersistenceOptions.FromConfiguration(Configuration(("XiHan:Persistence:EntityFrameworkCore:EnsureCreated", "yes"))));
    }

    [Fact]
    public async Task ApplyPersistenceConventions_ConfiguresConcurrencyTokenAndIndexes()
    {
        await using var host = await _database.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>().Model;

        var stamp = model.FindEntityType(typeof(ConformanceOrder))!.FindProperty(nameof(ConformanceOrder.ConcurrencyStamp))!;
        Assert.True(stamp.IsConcurrencyToken);
        Assert.Equal(32, stamp.GetMaxLength());
        Assert.Contains(model.FindEntityType(typeof(ConformanceNote))!.GetIndexes(), static i => i.Properties.Single().Name == nameof(ConformanceNote.TenantId));
        Assert.Contains(model.FindEntityType(typeof(ConformanceOrder))!.GetIndexes(), static i => i.Properties.Single().Name == nameof(ConformanceOrder.IsDeleted));
        Assert.Null(model.FindEntityType(typeof(ConformanceOrder))!.FindProperty(nameof(ConformanceOrder.DomainEvents)));
    }

    [Fact]
    public async Task Update_WithStaleDetachedEntity_ReturnsConcurrencyConflict()
    {
        await using var host = await _database.StartHostAsync();
        var id = Guid.CreateVersion7();
        (await RunAsync(host, ctx => Repository(ctx).AddAsync(new ConformanceOrder { Id = id, Name = "v1" }, ctx.CancellationToken))).ThrowIfFailure();

        ConformanceOrder? stale = null;
        (await RunAsync(host, async ctx =>
        {
            var loaded = (await Repository(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow();
            stale = new ConformanceOrder { Id = id, Name = "stale", ConcurrencyStamp = loaded.ConcurrencyStamp, CreatedAt = loaded.CreatedAt };
            return Result.Success();
        })).ThrowIfFailure();

        (await RunAsync(host, async ctx =>
        {
            var loaded = (await Repository(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow();
            loaded.Name = "v2";
            return await Repository(ctx).UpdateAsync(loaded, ctx.CancellationToken);
        })).ThrowIfFailure();

        var conflict = await RunAsync(host, ctx => Repository(ctx).UpdateAsync(stale!, ctx.CancellationToken));

        Assert.Equal(PersistenceErrors.Concurrency, conflict.Error?.Code);
        ConformanceOrder? current = null;
        (await RunAsync(host, async ctx =>
        {
            current = (await Repository(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow();
            return Result.Success();
        })).ThrowIfFailure();
        Assert.Equal("v2", current!.Name);
    }

    [Fact]
    public async Task Update_WithFreshDetachedEntity_CopiesValuesAndAuditBack()
    {
        await using var host = await _database.StartHostAsync();
        var id = Guid.CreateVersion7();
        (await RunAsync(host, ctx => Repository(ctx).AddAsync(new ConformanceOrder { Id = id, Name = "v1" }, ctx.CancellationToken))).ThrowIfFailure();

        ConformanceOrder? detached = null;
        (await RunAsync(host, async ctx =>
        {
            var loaded = (await Repository(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow();
            detached = new ConformanceOrder { Id = id, Name = "detached", Amount = 3, ConcurrencyStamp = loaded.ConcurrencyStamp, CreatedAt = loaded.CreatedAt };
            return Result.Success();
        })).ThrowIfFailure();

        var updated = await RunAsync(host, ctx => Repository(ctx).UpdateAsync(detached!, ctx.CancellationToken));

        Assert.True(updated.IsSuccess, updated.ToString());
        Assert.NotNull(detached!.ModifiedAt);
        ConformanceOrder? current = null;
        (await RunAsync(host, async ctx =>
        {
            current = (await Repository(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow();
            return Result.Success();
        })).ThrowIfFailure();
        Assert.Equal("detached", current!.Name);
        Assert.Equal(3, current.Amount);
        Assert.Equal(detached.ConcurrencyStamp, current.ConcurrencyStamp);
    }

    [Fact]
    public async Task ReadOnlyUnitOfWork_RejectsWritesImmediately()
    {
        await using var host = await _database.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true), CancellationToken.None);
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>();

        var added = await repository.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7() }, CancellationToken.None);

        Assert.Equal(PersistenceErrors.InvalidUnitOfWorkState, added.Error?.Code);
        Assert.True((await unitOfWork.CommitAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal(UnitOfWorkState.Committed, unitOfWork.State);
    }

    [Fact]
    public async Task UnitOfWork_EnforcesStateMachine()
    {
        await using var host = await _database.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var early = await Assert.ThrowsAsync<FrameworkException>(() => unitOfWork.CommitAsync(CancellationToken.None));
        Assert.Equal(PersistenceErrors.InvalidUnitOfWorkState, early.Code);

        await unitOfWork.BeginAsync(new UnitOfWorkOptions("acme"), CancellationToken.None);
        Assert.Equal("acme", unitOfWork.TenantId);
        await Assert.ThrowsAsync<FrameworkException>(() => unitOfWork.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None));

        await unitOfWork.RollbackAsync(CancellationToken.None);
        Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
        await Assert.ThrowsAsync<FrameworkException>(() => unitOfWork.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Feature_WithoutEnsureCreated_LeavesSchemaToApplication()
    {
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
            [new("XiHan:Bindings:db", _database.ConnectionString)],
            services => services.AddRepository<ConformanceOrder, Guid>());
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>();

        Assert.False(await context.Database.GetService<IRelationalDatabaseCreator>().HasTablesAsync());
    }

    private static IRepository<ConformanceOrder, Guid> Repository(InvocationContext context)
        => context.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>();

    private static async Task<Result> RunAsync(TestHost host, Func<InvocationContext, Task<Result>> action)
        => await host.InvokeAsync(async context => context.SetOutcome(await action(context)));
}
