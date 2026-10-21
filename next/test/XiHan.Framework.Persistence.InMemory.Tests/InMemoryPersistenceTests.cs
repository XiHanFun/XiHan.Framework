// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Domain;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory.Tests;

public sealed class InMemoryPersistenceTests
{
    private sealed class Widget : IEntity<Guid>
    {
        public Guid Id { get; init; }

        public string Name { get; set; } = string.Empty;
    }

    private static readonly DateTimeOffset _now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    private static (InMemoryStore Store, InMemoryUnitOfWork UnitOfWork, InMemoryRepository<Widget, Guid> Repository) Arrange(int capacity = 100)
    {
        var store = new InMemoryStore(new InMemoryPersistenceOptions { MaxEntitiesPerType = capacity });
        return Open(store);
    }

    private static (InMemoryStore Store, InMemoryUnitOfWork UnitOfWork, InMemoryRepository<Widget, Guid> Repository) Open(InMemoryStore store)
    {
        var unitOfWork = new InMemoryUnitOfWork(store);
        return (store, unitOfWork, new InMemoryRepository<Widget, Guid>(store, unitOfWork, new FakeTimeProvider(_now)));
    }

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        Assert.Equal(100_000, InMemoryPersistenceOptions.FromConfiguration(Configuration()).MaxEntitiesPerType);
        Assert.Equal(42, InMemoryPersistenceOptions.FromConfiguration(Configuration(("XiHan:Persistence:InMemory:MaxEntitiesPerType", "42"))).MaxEntitiesPerType);

        Assert.Throws<ArgumentException>(static () => InMemoryPersistenceOptions.FromConfiguration(Configuration(("XiHan:Persistence:InMemory:MaxEntitiesPerType", "0"))));
        Assert.Throws<ArgumentException>(static () => InMemoryPersistenceOptions.FromConfiguration(Configuration(("XiHan:Persistence:InMemory:MaxEntitiesPerType", "many"))));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new InMemoryPersistenceOptions { MaxEntitiesPerType = 0 });
    }

    [Fact]
    public async Task Commit_BeyondCapacity_FailsWholeBatch_AndLeavesTableUntouched()
    {
        var (store, first, repository) = Arrange(capacity: 2);
        await first.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await repository.AddAsync(new Widget { Id = Guid.NewGuid() }, CancellationToken.None);
        await repository.AddAsync(new Widget { Id = Guid.NewGuid() }, CancellationToken.None);
        Assert.True((await first.CommitAsync(CancellationToken.None)).IsSuccess);

        var (_, second, secondRepository) = Open(store);
        await second.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await secondRepository.AddAsync(new Widget { Id = Guid.NewGuid() }, CancellationToken.None);
        var commit = await second.CommitAsync(CancellationToken.None);

        Assert.True(commit.IsFailure);
        Assert.Equal(PersistenceErrors.CapacityExceeded, commit.Error!.Code);
        Assert.Equal(UnitOfWorkState.RolledBack, second.State);
        Assert.Equal(2, store.Table<Widget, Guid>().Count);
    }

    [Fact]
    public async Task Commit_ReplacingExistingRows_DoesNotCountAgainstCapacity()
    {
        var (store, first, repository) = Arrange(capacity: 1);
        var id = Guid.NewGuid();
        await first.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await repository.AddAsync(new Widget { Id = id, Name = "v1" }, CancellationToken.None);
        Assert.True((await first.CommitAsync(CancellationToken.None)).IsSuccess);

        var (_, second, secondRepository) = Open(store);
        await second.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        var existing = (await secondRepository.FindAsync(id, CancellationToken.None)).ValueOrThrow();
        existing.Name = "v2";
        Assert.True((await secondRepository.UpdateAsync(existing, CancellationToken.None)).IsSuccess);

        Assert.True((await second.CommitAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal("v2", store.Table<Widget, Guid>().Find(id).ValueOrThrow().Name);
    }

    [Fact]
    public async Task ReadOnlyUnitOfWork_RejectsWritesAtCommit()
    {
        var (_, unitOfWork, repository) = Arrange();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true), CancellationToken.None);
        await repository.AddAsync(new Widget { Id = Guid.NewGuid() }, CancellationToken.None);

        var commit = await unitOfWork.CommitAsync(CancellationToken.None);

        Assert.True(commit.IsFailure);
        Assert.Equal(PersistenceErrors.InvalidUnitOfWorkState, commit.Error!.Code);
        Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
    }

    [Fact]
    public async Task UnitOfWork_EnforcesStateMachine()
    {
        var (_, unitOfWork, _) = Arrange();
        Assert.Equal(UnitOfWorkState.NotStarted, unitOfWork.State);

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
    public async Task Repository_OutsideActiveUnitOfWork_Throws()
    {
        var (_, unitOfWork, repository) = Arrange();
        var widget = new Widget { Id = Guid.NewGuid() };

        var thrown = await Assert.ThrowsAsync<FrameworkException>(async () => await repository.FindAsync(widget.Id, CancellationToken.None));
        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, thrown.Code);
        await Assert.ThrowsAsync<FrameworkException>(() => repository.AddAsync(widget, CancellationToken.None));
        await Assert.ThrowsAsync<FrameworkException>(() => repository.ListAsync(EntityQuery.All<Widget>(), CancellationToken.None));

        await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);
        await Assert.ThrowsAsync<FrameworkException>(() => repository.AddAsync(widget, CancellationToken.None));
    }

    [Fact]
    public async Task Rollback_DiscardsOverlay()
    {
        var (store, unitOfWork, repository) = Arrange();
        await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        var id = Guid.NewGuid();
        await repository.AddAsync(new Widget { Id = id }, CancellationToken.None);
        Assert.True((await repository.FindAsync(id, CancellationToken.None)).HasValue);

        await unitOfWork.RollbackAsync(CancellationToken.None);

        Assert.Equal(0, store.Table<Widget, Guid>().Count);
    }

    [Fact]
    public async Task ConcurrentUnitsOfWork_CommitIndependently()
    {
        var store = new InMemoryStore(new InMemoryPersistenceOptions());
        const int writers = 32;

        var results = await Task.WhenAll(Enumerable.Range(0, writers).Select(async i =>
        {
            await Task.Yield();
            var (_, unitOfWork, repository) = Open(store);
            await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
            await repository.AddAsync(new Widget { Id = Guid.NewGuid(), Name = $"w{i}" }, CancellationToken.None);
            return await unitOfWork.CommitAsync(CancellationToken.None);
        }));

        Assert.All(results, static r => Assert.True(r.IsSuccess));
        Assert.Equal(writers, store.Table<Widget, Guid>().Count);
    }

    [Fact]
    public async Task Store_Clear_DropsAllTables()
    {
        var (store, unitOfWork, repository) = Arrange();
        await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await repository.AddAsync(new Widget { Id = Guid.NewGuid() }, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);
        Assert.Equal(1, store.Table<Widget, Guid>().Count);

        store.Clear();

        Assert.Equal(0, store.Table<Widget, Guid>().Count);
    }
}
