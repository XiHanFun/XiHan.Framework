// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory.Tests;

/// <summary>
/// 内存 Provider 独有的行为
/// </summary>
/// <remarks>
/// 选项解析、容量上限、只读工作单元、状态机、覆盖层回滚、读写都复制、提交时按首次读到的状态校验（并发新增同一标识、行被改或被删）且跨表原子、并发提交。一致性套件覆盖的通用行为不在此重复
/// </remarks>
public sealed class InMemoryPersistenceTests
{
    /// <summary>
    /// 不实现任何协议的测试实体
    /// </summary>
    private sealed class Widget : IEntity<Guid>
    {
        /// <inheritdoc />
        public Guid Id { get; init; }

        /// <summary>
        /// 名称
        /// </summary>
        /// <remarks>
        /// 用于验证更新后的值
        /// </remarks>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 注入仓储的固定时刻
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 用键值对构造内存配置
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>
    /// 新建指定容量的存储，并在其上打开一个工作单元与仓储
    /// </summary>
    private static (InMemoryStore Store, InMemoryUnitOfWork UnitOfWork, InMemoryRepository<Widget, Guid> Repository) Arrange(int capacity = 100)
    {
        var store = new InMemoryStore(new InMemoryPersistenceOptions { MaxEntitiesPerType = capacity });
        return Open(store);
    }

    /// <summary>
    /// 在已有存储上打开一个新的工作单元与仓储
    /// </summary>
    /// <remarks>
    /// 用于模拟第二个作用域
    /// </remarks>
    private static (InMemoryStore Store, InMemoryUnitOfWork UnitOfWork, InMemoryRepository<Widget, Guid> Repository) Open(InMemoryStore store)
    {
        var unitOfWork = new InMemoryUnitOfWork(store);
        return (store, unitOfWork, new InMemoryRepository<Widget, Guid>(store, unitOfWork, new FakeTimeProvider(_now)));
    }

    /// <summary>
    /// 在已有工作单元上打开订单仓储
    /// </summary>
    /// <remarks>
    /// 订单带并发戳，用来验证提交时的戳校验
    /// </remarks>
    private static InMemoryRepository<ConformanceOrder, Guid> Orders(InMemoryStore store, InMemoryUnitOfWork unitOfWork)
        => new(store, unitOfWork, new FakeTimeProvider(_now));

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
    public async Task NamedConnections_AreNotSupported_AtRegistrationOrBegin()
    {
        // 内存实现没有连接：实体登记到命名连接在装配时、绑定连接的工作单元在开始时明确拒绝，不静默落在唯一的存储上
        var services = new ServiceCollection();
        services.AddRepository<Widget, Guid>("leases");
        var registered = Assert.Throws<FrameworkException>(() => services.GetRepositoryRegistrations().ApplyTo(services, new InMemoryPersistenceFeature()));
        Assert.Equal(PersistenceErrors.NotSupported, registered.Code);

        var (_, unitOfWork, _) = Arrange();
        var begun = await Assert.ThrowsAsync<FrameworkException>(() => unitOfWork.BeginAsync(new UnitOfWorkOptions(Connection: "leases"), CancellationToken.None));
        Assert.Equal(PersistenceErrors.NotSupported, begun.Code);
        Assert.Equal(UnitOfWorkState.NotStarted, unitOfWork.State);
    }

    [Fact]
    public async Task Commit_BeyondCapacity_FailsWholeBatch_AndLeavesTableUntouched()
    {
        var (store, first, repository) = Arrange(capacity: 2);
        await first.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await repository.AddAsync(new Widget { Id = Guid.NewGuid() }, CancellationToken.None);
        await repository.AddAsync(new Widget { Id = Guid.NewGuid() }, CancellationToken.None);
        Assert.True((await first.CommitAsync(CancellationToken.None)).IsSuccess);

        // 容量在提交时才校验：覆盖层允许写入第三条，提交整批失败且表保持两条
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
    public async Task ReadOnlyUnitOfWork_RejectsWritesImmediately_AndCommitReturnsTheRejection()
    {
        var (store, unitOfWork, repository) = Arrange();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true), CancellationToken.None);
        var id = Guid.NewGuid();

        var added = await repository.AddAsync(new Widget { Id = id }, CancellationToken.None);

        // 写入时就拒绝、不进覆盖层：同一工作单元里也查不到
        Assert.Equal(PersistenceErrors.ReadOnly, added.Error?.Code);
        Assert.False((await repository.FindAsync(id, CancellationToken.None)).HasValue);
        Assert.Empty(await repository.AddRangeAsync([], CancellationToken.None));

        var commit = await unitOfWork.CommitAsync(CancellationToken.None);

        Assert.Equal(PersistenceErrors.ReadOnly, commit.Error?.Code);
        Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
        Assert.False(store.Table<Widget, Guid>().Find(id).HasValue);
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

        // 提交之后工作单元不再活动，仓储同样拒绝
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

        // 每个写入者各自一个工作单元，Task.Yield 让它们真正交错执行，验证表的锁能让各自的提交互不干扰
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
    public async Task Reads_ReturnCopies_ChangesNotWrittenBackNeverReachTheStore()
    {
        var (store, first, repository) = Arrange();
        var id = Guid.NewGuid();
        await first.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await repository.AddAsync(new Widget { Id = id, Name = "v1" }, CancellationToken.None);
        Assert.True((await first.CommitAsync(CancellationToken.None)).IsSuccess);

        // 表的公开读取交出的也是副本
        store.Table<Widget, Guid>().Find(id).ValueOrThrow().Name = "tampered";
        store.Table<Widget, Guid>().Snapshot()[0].Name = "tampered";

        // 读出的实例改了却没经仓储写回：本工作单元随后的读取、提交后的表都看不到
        var (_, second, secondRepository) = Open(store);
        await second.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        (await secondRepository.FindAsync(id, CancellationToken.None)).ValueOrThrow().Name = "v2";
        (await secondRepository.ListAsync(EntityQuery.All<Widget>(), CancellationToken.None))[0].Name = "v2";
        (await secondRepository.FindManyAsync([id], CancellationToken.None))[0].Name = "v2";
        Assert.Equal("v1", (await secondRepository.FindAsync(id, CancellationToken.None)).ValueOrThrow().Name);
        Assert.True((await second.CommitAsync(CancellationToken.None)).IsSuccess);

        Assert.Equal("v1", store.Table<Widget, Guid>().Find(id).ValueOrThrow().Name);
    }

    [Fact]
    public async Task Writes_StoreCopies_CallerChangesAfterTheWriteDoNotLeak()
    {
        var (store, unitOfWork, repository) = Arrange();
        var id = Guid.NewGuid();
        var widget = new Widget { Id = id, Name = "v1" };
        await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await repository.AddAsync(widget, CancellationToken.None);

        // 写入之后再改调用方实例：覆盖层与表里都还是写入时的值
        widget.Name = "changed";
        Assert.Equal("v1", (await repository.FindAsync(id, CancellationToken.None)).ValueOrThrow().Name);
        Assert.True((await unitOfWork.CommitAsync(CancellationToken.None)).IsSuccess);
        widget.Name = "changed-again";

        Assert.Equal("v1", store.Table<Widget, Guid>().Find(id).ValueOrThrow().Name);
    }

    [Fact]
    public async Task Commit_AddOfAnIdCommittedMeanwhile_ReturnsDuplicate_AndKeepsTheFirstRow()
    {
        var store = new InMemoryStore(new InMemoryPersistenceOptions());
        var id = Guid.NewGuid();
        var (_, first, firstRepository) = Open(store);
        var (_, second, secondRepository) = Open(store);
        await first.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await second.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);

        // 两个覆盖层都看不到对方未提交的新增，各自的新增都成功
        Assert.True((await firstRepository.AddAsync(new Widget { Id = id, Name = "first" }, CancellationToken.None)).IsSuccess);
        Assert.True((await secondRepository.AddAsync(new Widget { Id = id, Name = "second" }, CancellationToken.None)).IsSuccess);
        Assert.True((await first.CommitAsync(CancellationToken.None)).IsSuccess);
        var commit = await second.CommitAsync(CancellationToken.None);

        Assert.Equal(PersistenceErrors.Duplicate, commit.Error?.Code);
        Assert.Equal(UnitOfWorkState.RolledBack, second.State);
        Assert.Equal("first", store.Table<Widget, Guid>().Find(id).ValueOrThrow().Name);
    }

    [Fact]
    public async Task Commit_UpdateOfARowChangedOrRemovedMeanwhile_ReturnsConcurrency()
    {
        var store = new InMemoryStore(new InMemoryPersistenceOptions());
        var orderId = Guid.NewGuid();
        var widgetId = Guid.NewGuid();
        var (_, seed, widgets) = Open(store);
        await seed.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await Orders(store, seed).AddAsync(new ConformanceOrder { Id = orderId, Name = "v1" }, CancellationToken.None);
        await widgets.AddAsync(new Widget { Id = widgetId, Name = "v1" }, CancellationToken.None);
        Assert.True((await seed.CommitAsync(CancellationToken.None)).IsSuccess);

        // 带并发戳的行：两个工作单元都基于同一个戳更新，先提交的生效，后提交的冲突
        var (_, first, _) = Open(store);
        var (_, second, _) = Open(store);
        await first.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await second.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await RenameAsync(Orders(store, first), "first");
        await RenameAsync(Orders(store, second), "second");
        Assert.True((await first.CommitAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal(PersistenceErrors.Concurrency, (await second.CommitAsync(CancellationToken.None)).Error?.Code);
        Assert.Equal("first", store.Table<ConformanceOrder, Guid>().Find(orderId).ValueOrThrow().Name);

        // 不带并发戳的行：更新期间被别的工作单元物理删除，提交冲突而不是把它写回来
        var (_, updater, updaterRepository) = Open(store);
        var (_, remover, removerRepository) = Open(store);
        await updater.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await remover.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        var widget = (await updaterRepository.FindAsync(widgetId, CancellationToken.None)).ValueOrThrow();
        widget.Name = "updated";
        Assert.True((await updaterRepository.UpdateAsync(widget, CancellationToken.None)).IsSuccess);
        Assert.True((await removerRepository.RemoveAsync((await removerRepository.FindAsync(widgetId, CancellationToken.None)).ValueOrThrow(), CancellationToken.None)).IsSuccess);
        Assert.True((await remover.CommitAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal(PersistenceErrors.Concurrency, (await updater.CommitAsync(CancellationToken.None)).Error?.Code);
        Assert.False(store.Table<Widget, Guid>().Find(widgetId).HasValue);

        // 读出改名再更新
        async Task RenameAsync(InMemoryRepository<ConformanceOrder, Guid> orders, string name)
        {
            var order = (await orders.FindAsync(orderId, CancellationToken.None)).ValueOrThrow();
            order.Name = name;
            Assert.True((await orders.UpdateAsync(order, CancellationToken.None)).IsSuccess);
        }
    }

    [Fact]
    public async Task Commit_ConflictInOneTable_LeavesEveryTableUntouched()
    {
        var store = new InMemoryStore(new InMemoryPersistenceOptions());
        var orderId = Guid.NewGuid();
        var (_, first, _) = Open(store);
        var (_, second, secondWidgets) = Open(store);
        await first.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
        await second.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);

        // 第二个工作单元写两张表，其中订单与第一个工作单元先提交的新增撞上同一标识
        await Orders(store, first).AddAsync(new ConformanceOrder { Id = orderId, Name = "first" }, CancellationToken.None);
        await secondWidgets.AddAsync(new Widget { Id = Guid.NewGuid(), Name = "w" }, CancellationToken.None);
        await Orders(store, second).AddAsync(new ConformanceOrder { Id = orderId, Name = "second" }, CancellationToken.None);
        Assert.True((await first.CommitAsync(CancellationToken.None)).IsSuccess);

        Assert.Equal(PersistenceErrors.Duplicate, (await second.CommitAsync(CancellationToken.None)).Error?.Code);
        Assert.Equal(0, store.Table<Widget, Guid>().Count);
        Assert.Equal("first", store.Table<ConformanceOrder, Guid>().Find(orderId).ValueOrThrow().Name);
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
