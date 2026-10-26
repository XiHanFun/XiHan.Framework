// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

namespace XiHan.Framework.Persistence.Conformance;

/// <summary>
/// <c>persistence.relational@1</c> Provider 的一致性套件（含批量用例）。继承并实现 <see cref="CreateFeature"/>，全部用例随之执行。
/// 每个用例都以 <see cref="TestHost"/> 走完整管道：工作单元由 Transaction 阶段的中间件开启、提交或回滚。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class RepositoryConformance
{
    /// <summary>
    /// 套件时钟的起点。
    /// </summary>
    public static DateTimeOffset Epoch { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 注入宿主的时钟；审计字段的期望值据此计算。
    /// </summary>
    protected ConformanceClock Clock { get; } = new(Epoch);

    /// <summary>
    /// 宿主配置；Provider 需要的连接等在此提供。
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> Configuration => [];

    /// <summary>
    /// 创建被测 Provider 的特性。每个用例调用一次。
    /// </summary>
    protected abstract IFeature CreateFeature();

    /// <summary>
    /// 附加服务注册。
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// 附加应用配置。
    /// </summary>
    protected virtual void ConfigureApp(AppBuilder builder)
    {
    }

    /// <summary>
    /// 用例 1：新增在同一工作单元内可读，提交后对新调用可见，并写入创建时间与并发戳。
    /// </summary>
    [Fact]
    public async Task Add_IsReadableInSameUnitOfWork_AndVisibleAfterCommit()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        Maybe<ConformanceOrder> inside = default;
        IUnitOfWork? unitOfWork = null;

        // 把作用域内的工作单元引用带出管道，调用结束后才能断言它的终态
        var added = await RunAsync(host, async context =>
        {
            unitOfWork = context.Services.GetRequiredService<IUnitOfWork>();
            var repository = Repository<ConformanceOrder, Guid>(context);
            var result = await repository.AddAsync(new ConformanceOrder { Id = id, Name = "first", Amount = 10 }, context.CancellationToken).ConfigureAwait(false);
            inside = await repository.FindAsync(id, context.CancellationToken).ConfigureAwait(false);
            return result;
        }).ConfigureAwait(false);

        Assert.True(added.IsSuccess, added.ToString());
        Assert.True(inside.HasValue);
        Assert.NotNull(unitOfWork);
        Assert.Equal(UnitOfWorkState.Committed, unitOfWork.State);

        var order = (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("first", order.Name);
        Assert.Equal(Clock.GetUtcNow(), order.CreatedAt);
        Assert.Null(order.ModifiedAt);
        Assert.False(string.IsNullOrEmpty(order.ConcurrencyStamp));
    }

    /// <summary>
    /// 用例 2：未提交的写入对其他工作单元不可见。
    /// </summary>
    [Fact]
    public async Task UncommittedWrite_IsInvisibleToOtherUnitOfWork()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        Maybe<ConformanceOrder> seenByOther = default;

        var result = await RunAsync(host, async context =>
        {
            await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id }, context.CancellationToken).ConfigureAwait(false);

            // 在当前调用尚未提交时，用另一个作用域（即另一个工作单元与连接）读取同一标识，模拟并发的第二个调用
            await using var other = context.Services.CreateAsyncScope();
            var otherUnitOfWork = other.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await otherUnitOfWork.BeginAsync(UnitOfWorkOptions.Default, context.CancellationToken).ConfigureAwait(false);
            seenByOther = await other.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>().FindAsync(id, context.CancellationToken).ConfigureAwait(false);
            await otherUnitOfWork.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.False(seenByOther.HasValue);
        Assert.True((await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 3：标识重复，无论在同一工作单元还是跨工作单元，都返回 <see cref="PersistenceErrors.Duplicate"/>。
    /// </summary>
    [Fact]
    public async Task Add_DuplicateId_ReturnsDuplicate()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();

        var across = await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id }, context.CancellationToken)).ConfigureAwait(false);
        Assert.Equal(PersistenceErrors.Duplicate, across.Error?.Code);

        // 同一工作单元内第二次新增失败，整个调用以失败结果回滚，第一次新增也不应落库
        var other = Guid.CreateVersion7();
        var within = await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            (await repository.AddAsync(new ConformanceOrder { Id = other }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            return await repository.AddAsync(new ConformanceOrder { Id = other }, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        Assert.Equal(PersistenceErrors.Duplicate, within.Error?.Code);
        Assert.False((await FindAsync<ConformanceOrder, Guid>(host, other).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 4：更新写入修改时间并刷新并发戳。
    /// </summary>
    [Fact]
    public async Task Update_WritesModificationTime_AndRefreshesStamp()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id, Name = "v1" }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        // 只记录戳的值：内存实现返回的是同一对象引用，不能拿对象本身做前后对比
        var originalStamp = (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow().ConcurrencyStamp;
        Clock.Advance(TimeSpan.FromHours(1));

        var updated = await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            var order = (await repository.FindAsync(id, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            order.Name = "v2";
            return await repository.UpdateAsync(order, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        Assert.True(updated.IsSuccess, updated.ToString());
        var current = (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("v2", current.Name);
        Assert.Equal(Clock.GetUtcNow(), current.ModifiedAt);
        Assert.Equal(Epoch, current.CreatedAt);
        Assert.NotEqual(originalStamp, current.ConcurrencyStamp);
    }

    /// <summary>
    /// 用例 5：更新或删除不存在的实体返回 <see cref="PersistenceErrors.NotFound"/>。
    /// </summary>
    [Fact]
    public async Task Update_And_Remove_Missing_ReturnNotFound()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var ghost = new ConformanceOrder { Id = Guid.CreateVersion7() };

        var updated = await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).UpdateAsync(ghost, context.CancellationToken)).ConfigureAwait(false);
        var removed = await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).RemoveAsync(ghost, context.CancellationToken)).ConfigureAwait(false);

        Assert.Equal(PersistenceErrors.NotFound, updated.Error?.Code);
        Assert.Equal(PersistenceErrors.NotFound, removed.Error?.Code);
    }

    /// <summary>
    /// 用例 6：软删除实体被打标并隐藏，只有 <see cref="EntityQuery{T}.IncludeDeleted"/> 能看到；之后更新与再次删除返回 NotFound。
    /// </summary>
    [Fact]
    public async Task Remove_SoftDeletable_HidesEntity_UntilIncludeDeleted()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        Clock.Advance(TimeSpan.FromMinutes(1));

        var removed = await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            var order = (await repository.FindAsync(id, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            return await repository.RemoveAsync(order, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        Assert.True(removed.IsSuccess, removed.ToString());

        Assert.False((await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).HasValue);
        Assert.Equal(0, await CountAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>()).ConfigureAwait(false));

        var deleted = await ListAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>() with { IncludeDeleted = true }).ConfigureAwait(false);
        var tombstone = Assert.Single(deleted);
        Assert.True(tombstone.IsDeleted);
        Assert.Equal(Clock.GetUtcNow(), tombstone.DeletedAt);

        // 已软删除的实体对更新与删除都视为不存在，即使调用方手里还拿着它
        var updated = await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).UpdateAsync(tombstone, context.CancellationToken)).ConfigureAwait(false);
        var removedAgain = await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).RemoveAsync(tombstone, context.CancellationToken)).ConfigureAwait(false);
        Assert.Equal(PersistenceErrors.NotFound, updated.Error?.Code);
        Assert.Equal(PersistenceErrors.NotFound, removedAgain.Error?.Code);
    }

    /// <summary>
    /// 用例 7：不实现软删除协议的实体被物理删除。
    /// </summary>
    [Fact]
    public async Task Remove_PlainEntity_DeletesPhysically()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        (await RunAsync(host, context => Repository<ConformanceItem, int>(context).AddAsync(new ConformanceItem { Id = 7, Label = "seven" }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();

        var removed = await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceItem, int>(context);
            var item = (await repository.FindAsync(7, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            return await repository.RemoveAsync(item, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        Assert.True(removed.IsSuccess, removed.ToString());
        Assert.False((await FindAsync<ConformanceItem, int>(host, 7).ConfigureAwait(false)).HasValue);
        // IncludeDeleted 也看不到：物理删除没有墓碑
        Assert.Equal(0, await CountAsync<ConformanceItem, int>(host, EntityQuery.All<ConformanceItem>() with { IncludeDeleted = true }).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 8：调用结果为失败时回滚，失败结果原样返回。
    /// </summary>
    [Fact]
    public async Task FailureOutcome_RollsBack()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        var rejection = Error.Literal("CONFORMANCE_VALIDATION_001", "Rejected by the business rule.");
        IUnitOfWork? unitOfWork = null;

        var result = await RunAsync(host, async context =>
        {
            unitOfWork = context.Services.GetRequiredService<IUnitOfWork>();
            (await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            return rejection;
        }).ConfigureAwait(false);

        Assert.Equal(rejection, result.Error);
        Assert.NotNull(unitOfWork);
        Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
        Assert.False((await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 9：未处理异常回滚，并由内核转为 <see cref="KernelErrors.Unexpected"/>。
    /// </summary>
    [Fact]
    public async Task Exception_RollsBack_AndBecomesUnexpectedOutcome()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        IUnitOfWork? unitOfWork = null;

        var result = await RunAsync(host, async context =>
        {
            unitOfWork = context.Services.GetRequiredService<IUnitOfWork>();
            (await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            throw new InvalidOperationException("The handler crashed.");
        }).ConfigureAwait(false);

        Assert.Equal(KernelErrors.Unexpected, result.Error?.Code);
        Assert.NotNull(unitOfWork);
        Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
        Assert.False((await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 10：多租户实体没有当前租户时拒绝新增；不同租户互不可见；无租户的调用看不到任何多租户实体。
    /// </summary>
    [Fact]
    public async Task MultiTenant_RequiresTenant_AndIsolatesTenants()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();

        var withoutTenant = await RunAsync(host, context => Repository<ConformanceNote, Guid>(context).AddAsync(new ConformanceNote { Id = id, Text = "hi" }, context.CancellationToken)).ConfigureAwait(false);
        Assert.Equal(PersistenceErrors.TenantRequired, withoutTenant.Error?.Code);

        var withTenant = await RunAsync(host, context => Repository<ConformanceNote, Guid>(context).AddAsync(new ConformanceNote { Id = id, Text = "hi" }, context.CancellationToken), tenantId: "t1").ConfigureAwait(false);
        Assert.True(withTenant.IsSuccess, withTenant.ToString());

        var ownNote = (await FindAsync<ConformanceNote, Guid>(host, id, tenantId: "t1").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("t1", ownNote.TenantId);
        Assert.Equal(1, await CountAsync<ConformanceNote, Guid>(host, EntityQuery.All<ConformanceNote>(), tenantId: "t1").ConfigureAwait(false));

        Assert.False((await FindAsync<ConformanceNote, Guid>(host, id, tenantId: "t2").ConfigureAwait(false)).HasValue);
        Assert.Equal(0, await CountAsync<ConformanceNote, Guid>(host, EntityQuery.All<ConformanceNote>(), tenantId: "t2").ConfigureAwait(false));
        Assert.Equal(0, await CountAsync<ConformanceNote, Guid>(host, EntityQuery.All<ConformanceNote>()).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 11：查询按过滤、排序、分页返回；计数忽略分页。
    /// </summary>
    [Fact]
    public async Task List_AppliesFilterOrderingAndPaging_CountIgnoresPaging()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        (await RunAsync(host, async context => Fold(await Repository<ConformanceOrder, Guid>(context).AddRangeAsync(
            [.. Enumerable.Range(1, 5).Select(static amount => new ConformanceOrder { Id = Guid.CreateVersion7(), Name = $"o{amount}", Amount = amount })],
            context.CancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ThrowIfFailure();

        // Amount >= 2 命中 o2..o5，降序后跳过 1 条取 2 条得到 o4、o3；计数仍是全部命中的 4 条
        var query = EntityQuery.All<ConformanceOrder>()
            .Where(new Specification<ConformanceOrder>(static o => o.Amount >= 2))
            .OrderBy(static o => o.Amount, descending: true)
            .Page(1, 2);

        var page = await ListAsync<ConformanceOrder, Guid>(host, query).ConfigureAwait(false);
        Assert.Equal(["o4", "o3"], page.Select(static o => o.Name));
        Assert.Equal(4, await CountAsync<ConformanceOrder, Guid>(host, query).ConfigureAwait(false));

        var ascending = await ListAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>().OrderBy(static o => o.Amount)).ConfigureAwait(false);
        Assert.Equal(["o1", "o2", "o3", "o4", "o5"], ascending.Select(static o => o.Name));
        Assert.Equal(2, (await ListAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>().Page(0, 2)).ConfigureAwait(false)).Count);
    }

    /// <summary>
    /// 用例 12：在 Transaction 阶段之外使用仓储抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>。
    /// </summary>
    [Fact]
    public async Task Repository_OutsideTransactionStage_Throws()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        // 直接从作用域解析仓储而不经过管道：工作单元从未开始
        await using var scope = host.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>();

        var read = await Assert.ThrowsAsync<FrameworkException>(async () => await repository.FindAsync(Guid.CreateVersion7(), CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
        var write = await Assert.ThrowsAsync<FrameworkException>(() => repository.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7() }, CancellationToken.None)).ConfigureAwait(false);

        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, read.Code);
        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, write.Code);
    }

    /// <summary>
    /// 用例 13：Provider 注册 <see cref="IIdGenerator{TKey}"/>（Guid 版本 7），并在清单里声明 <c>persistence.relational@1</c>。
    /// </summary>
    [Fact]
    public async Task Provider_RegistersIdGenerator_AndDeclaresCapability()
    {
        var manifest = CreateFeature().Manifest;
        Assert.True(manifest.Validate().IsSuccess);
        Assert.Contains(manifest.Provides, static d => d.Id == PersistenceCapabilities.Relational && d.Version == PersistenceCapabilities.Version);

        await using var host = await StartAsync().ConfigureAwait(false);
        var generator = host.Services.GetRequiredService<IIdGenerator<Guid>>();
        Assert.Equal(7, generator.NewId().Version);
    }

    /// <summary>
    /// 用例 14：批量新增一次写入、读己之写，结果与入参一一对应；批内重复与跨工作单元的重复标识按项报告，其余项照常落库；
    /// 按标识集合查找只返回存在的，顺序按入参、重复只一次。
    /// </summary>
    [Fact]
    public async Task AddRange_ReportsPerItem_AndFindManyKeepsInputOrder()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var existing = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = existing, Name = "existing" }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();

        IReadOnlyList<Result> results = [];
        IReadOnlyList<ConformanceOrder> inside = [];
        var outcome = await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            // 第三项与第一项同标识（批内重复），第四项与已提交的行同标识（跨工作单元重复）
            results = await repository.AddRangeAsync(
            [
                new ConformanceOrder { Id = first, Name = "a", Amount = 1 },
                new ConformanceOrder { Id = second, Name = "b", Amount = 2 },
                new ConformanceOrder { Id = first, Name = "a-again" },
                new ConformanceOrder { Id = existing, Name = "existing-again" },
            ], context.CancellationToken).ConfigureAwait(false);
            inside = await repository.FindManyAsync([second, Guid.CreateVersion7(), first, second], context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(outcome.IsSuccess, outcome.ToString());
        Assert.Equal([true, true, false, false], results.Select(static r => r.IsSuccess));
        Assert.Equal(PersistenceErrors.Duplicate, results[2].Error?.Code);
        Assert.Equal(PersistenceErrors.Duplicate, results[3].Error?.Code);
        Assert.Equal(["b", "a"], inside.Select(static o => o.Name));

        var committed = await FindManyAsync<ConformanceOrder, Guid>(host, [first, second, existing]).ConfigureAwait(false);
        Assert.Equal(["a", "b", "existing"], committed.Select(static o => o.Name));
        Assert.All(committed, order =>
        {
            Assert.Equal(Clock.GetUtcNow(), order.CreatedAt);
            Assert.False(string.IsNullOrEmpty(order.ConcurrencyStamp));
        });
    }

    /// <summary>
    /// 用例 15：批量更新一次写入：写入修改时间并刷新并发戳；并发戳过期的游离实例返回 <see cref="PersistenceErrors.Concurrency"/>、不存在的返回 NotFound，其余项照常生效。
    /// </summary>
    [Fact]
    public async Task UpdateRange_RefreshesStamps_AndReportsStaleOrMissingPerItem()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        (await RunAsync(host, async context => Fold(await Repository<ConformanceOrder, Guid>(context).AddRangeAsync([new ConformanceOrder { Id = first, Name = "a" }, new ConformanceOrder { Id = second, Name = "b" }], context.CancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ThrowIfFailure();
        var stamps = (await FindManyAsync<ConformanceOrder, Guid>(host, [first, second]).ConfigureAwait(false)).Select(static o => o.ConcurrencyStamp).ToList();
        Clock.Advance(TimeSpan.FromHours(1));

        IReadOnlyList<Result> results = [];
        var outcome = await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            var found = await repository.FindManyAsync([first], context.CancellationToken).ConfigureAwait(false);
            found[0].Name = "a2";
            // 第二项是并发戳已过期的游离实例，第三项不存在
            results = await repository.UpdateRangeAsync(
            [
                found[0],
                new ConformanceOrder { Id = second, Name = "stale", ConcurrencyStamp = "stale", CreatedAt = Epoch },
                new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "ghost" },
            ], context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(outcome.IsSuccess, outcome.ToString());
        Assert.True(results[0].IsSuccess, results[0].ToString());
        Assert.Equal(PersistenceErrors.Concurrency, results[1].Error?.Code);
        Assert.Equal(PersistenceErrors.NotFound, results[2].Error?.Code);

        var current = await FindManyAsync<ConformanceOrder, Guid>(host, [first, second]).ConfigureAwait(false);
        Assert.Equal("a2", current[0].Name);
        Assert.Equal(Clock.GetUtcNow(), current[0].ModifiedAt);
        Assert.NotEqual(stamps[0], current[0].ConcurrencyStamp);
        Assert.Equal("b", current[1].Name);
        Assert.Equal(stamps[1], current[1].ConcurrencyStamp);
    }

    /// <summary>
    /// 用例 16：批量删除一次写入：软删除实体打标隐藏、普通实体物理删除；批内同一实体第二次与不存在的实体按项返回 NotFound，其余项照常生效。
    /// </summary>
    [Fact]
    public async Task RemoveRange_SoftDeletesOrDeletes_AndReportsMissingPerItem()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        (await RunAsync(host, async context =>
        {
            var orders = await Repository<ConformanceOrder, Guid>(context).AddRangeAsync([new ConformanceOrder { Id = first }, new ConformanceOrder { Id = second }], context.CancellationToken).ConfigureAwait(false);
            var items = await Repository<ConformanceItem, int>(context).AddRangeAsync([new ConformanceItem { Id = 1, Label = "one" }, new ConformanceItem { Id = 2, Label = "two" }], context.CancellationToken).ConfigureAwait(false);
            return Fold([.. orders, .. items]);
        }).ConfigureAwait(false)).ThrowIfFailure();
        Clock.Advance(TimeSpan.FromMinutes(1));

        IReadOnlyList<Result> orderResults = [];
        IReadOnlyList<Result> itemResults = [];
        var outcome = await RunAsync(host, async context =>
        {
            var orders = Repository<ConformanceOrder, Guid>(context);
            var found = await orders.FindManyAsync([first, second], context.CancellationToken).ConfigureAwait(false);
            // 第三项与第一项是同一实体，第四项不存在
            orderResults = await orders.RemoveRangeAsync([found[0], found[1], found[0], new ConformanceOrder { Id = Guid.CreateVersion7() }], context.CancellationToken).ConfigureAwait(false);
            var items = Repository<ConformanceItem, int>(context);
            itemResults = await items.RemoveRangeAsync(await items.FindManyAsync([1, 2], context.CancellationToken).ConfigureAwait(false), context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(outcome.IsSuccess, outcome.ToString());
        Assert.Equal([true, true, false, false], orderResults.Select(static r => r.IsSuccess));
        Assert.Equal(PersistenceErrors.NotFound, orderResults[2].Error?.Code);
        Assert.Equal(PersistenceErrors.NotFound, orderResults[3].Error?.Code);
        Assert.All(itemResults, static r => Assert.True(r.IsSuccess, r.ToString()));

        Assert.Empty(await FindManyAsync<ConformanceOrder, Guid>(host, [first, second]).ConfigureAwait(false));
        var tombstones = await ListAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>() with { IncludeDeleted = true }).ConfigureAwait(false);
        Assert.Equal(2, tombstones.Count);
        Assert.All(tombstones, tombstone =>
        {
            Assert.True(tombstone.IsDeleted);
            Assert.Equal(Clock.GetUtcNow(), tombstone.DeletedAt);
        });
        Assert.Equal(0, await CountAsync<ConformanceItem, int>(host, EntityQuery.All<ConformanceItem>() with { IncludeDeleted = true }).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 17：空集合不触后端、返回空列表；在 Transaction 阶段之外使用批量方法同样抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>。
    /// </summary>
    [Fact]
    public async Task BatchMethods_EmptyInputReturnsEmpty_AndRequireUnitOfWork()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var outcome = await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            Assert.Empty(await repository.FindManyAsync([], context.CancellationToken).ConfigureAwait(false));
            Assert.Empty(await repository.AddRangeAsync([], context.CancellationToken).ConfigureAwait(false));
            Assert.Empty(await repository.UpdateRangeAsync([], context.CancellationToken).ConfigureAwait(false));
            Assert.Empty(await repository.RemoveRangeAsync([], context.CancellationToken).ConfigureAwait(false));
            return Result.Success();
        }).ConfigureAwait(false);
        Assert.True(outcome.IsSuccess, outcome.ToString());

        // 直接从作用域解析仓储而不经过管道：工作单元从未开始
        await using var scope = host.Services.CreateAsyncScope();
        var detached = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>();
        var read = await Assert.ThrowsAsync<FrameworkException>(() => detached.FindManyAsync([Guid.CreateVersion7()], CancellationToken.None)).ConfigureAwait(false);
        var write = await Assert.ThrowsAsync<FrameworkException>(() => detached.AddRangeAsync([new ConformanceOrder { Id = Guid.CreateVersion7() }], CancellationToken.None)).ConfigureAwait(false);
        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, read.Code);
        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, write.Code);
    }

    /// <summary>
    /// 装配并启动宿主：被测特性、套件特性、三种套件实体的登记、套件时钟。
    /// </summary>
    protected Task<TestHost> StartAsync(Action<IServiceCollection>? extra = null) => TestHost.StartAsync(
        builder =>
        {
            builder.UseFeature(CreateFeature());
            builder.UseFeature<ConformanceFeature>();
            ConfigureApp(builder);
        },
        Configuration,
        services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceNote, Guid>();
            services.AddRepository<ConformanceItem, int>();
            ConfigureServices(services);
            extra?.Invoke(services);
        });

    /// <summary>
    /// 在一次完整管道调用内执行 <paramref name="action"/>，其返回值作为调用结果；<paramref name="tenantId"/> 经 Baggage 进入 Tenant 阶段。
    /// </summary>
    protected static async Task<Result> RunAsync(TestHost host, Func<InvocationContext, Task<Result>> action, string? tenantId = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(action);
        // 租户不直接写安全上下文，而是放进 Baggage 由 ConformanceFeature 的 Tenant 阶段中间件解析，与真实调用路径一致
        var baggage = tenantId is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = tenantId };
        return await host.InvokeAsync(async context => context.SetOutcome(await action(context).ConfigureAwait(false)), baggage: baggage).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 19：时间范围提示不是过滤条件——不分表的实体带 <c>Within</c> 查询仍返回范围之外的行，计数也不变；范围颠倒是编程错误。
    /// </summary>
    [Fact]
    public async Task List_WithTimeRangeHint_OnUnpartitionedEntity_ReturnsAllRows()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        (await RunAsync(host, async context => Fold(await Repository<ConformanceOrder, Guid>(context).AddRangeAsync(
            [new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "a", Amount = 1 }, new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "b", Amount = 2 }],
            context.CancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ThrowIfFailure();

        // 套件时钟从 Epoch 开始，两条都在 Epoch 当天创建；范围取远在创建时间之前的一天，仍然两条都返回
        var query = EntityQuery.All<ConformanceOrder>().Within(Epoch.AddYears(-1), Epoch.AddYears(-1).AddDays(1)).OrderBy(static o => o.Amount);
        Assert.Equal(["a", "b"], (await ListAsync<ConformanceOrder, Guid>(host, query).ConfigureAwait(false)).Select(static o => o.Name));
        Assert.Equal(2, await CountAsync<ConformanceOrder, Guid>(host, query).ConfigureAwait(false));
        Assert.Throws<ArgumentOutOfRangeException>(static () => EntityQuery.All<ConformanceOrder>().Within(Epoch, Epoch.AddDays(-1)));
    }

    /// <summary>
    /// 用例 20：聚合记录的领域事件在提交时交给派发器（新增与更新过的聚合都登记、同一实例一次、带当前租户），派发后缓冲清空；失败的调用回滚，不派发。
    /// </summary>
    [Fact]
    public async Task DomainEvents_AreDispatchedOnCommit_AndCleared()
    {
        var dispatcher = new RecordingDomainEventDispatcher();
        await using var host = await StartAsync(services => services.AddSingleton<IDomainEventDispatcher>(dispatcher)).ConfigureAwait(false);
        var first = new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "first" };
        first.Raise(new ConformanceEvent("first-created", Epoch));

        (await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            (await repository.AddAsync(first, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            (await repository.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "quiet" }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            // 同一实例新增后再更新只登记一次，两条事件在同一批里按记录顺序
            first.Raise(new ConformanceEvent("first-renamed", Epoch));
            first.Name = "renamed";
            return await repository.UpdateAsync(first, context.CancellationToken).ConfigureAwait(false);
        }, tenantId: "acme").ConfigureAwait(false)).ThrowIfFailure();

        var batch = Assert.Single(dispatcher.Batches);
        Assert.Equal(["first-created", "first-renamed"], batch.Events);
        Assert.Equal("acme", batch.TenantId);
        Assert.Empty(first.DomainEvents);

        var failed = await RunAsync(host, async context =>
        {
            var doomed = new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "doomed" };
            doomed.Raise(new ConformanceEvent("doomed", Epoch));
            (await Repository<ConformanceOrder, Guid>(context).AddAsync(doomed, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            return new Error("CONFORMANCE_VALIDATION_001", "conformance.rejected");
        }).ConfigureAwait(false);
        Assert.True(failed.IsFailure);
        Assert.Single(dispatcher.Batches);
    }

    /// <summary>
    /// 用例 21：派发器失败即回滚且调用结果是派发器的错误；没有注册派发器却有事件返回 <see cref="PersistenceErrors.EventsUndispatched"/> 并回滚；没有事件的调用不受影响。
    /// </summary>
    [Fact]
    public async Task DomainEvents_DispatchFailureRollsBack_AndMissingDispatcherIsRejected()
    {
        var dispatcher = new RecordingDomainEventDispatcher { Failure = new Error("CONFORMANCE_UNAVAILABLE_001", "conformance.dispatch_failed") };
        await using var failing = await StartAsync(services => services.AddSingleton<IDomainEventDispatcher>(dispatcher)).ConfigureAwait(false);
        var id = Guid.CreateVersion7();

        var outcome = await RunAsync(failing, async context =>
        {
            var order = new ConformanceOrder { Id = id, Name = "order" };
            order.Raise(new ConformanceEvent("created", Epoch));
            return await Repository<ConformanceOrder, Guid>(context).AddAsync(order, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        Assert.Equal("CONFORMANCE_UNAVAILABLE_001", outcome.Error?.Code);
        Assert.Empty(await ListAsync<ConformanceOrder, Guid>(failing, EntityQuery.All<ConformanceOrder>()).ConfigureAwait(false));

        await using var bare = await StartAsync().ConfigureAwait(false);
        var rejected = await RunAsync(bare, async context =>
        {
            var order = new ConformanceOrder { Id = id, Name = "order" };
            order.Raise(new ConformanceEvent("created", Epoch));
            return await Repository<ConformanceOrder, Guid>(context).AddAsync(order, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        Assert.Equal(PersistenceErrors.EventsUndispatched, rejected.Error?.Code);
        Assert.Empty(await ListAsync<ConformanceOrder, Guid>(bare, EntityQuery.All<ConformanceOrder>()).ConfigureAwait(false));

        (await RunAsync(bare, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id, Name = "plain" }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        Assert.Single(await ListAsync<ConformanceOrder, Guid>(bare, EntityQuery.All<ConformanceOrder>()).ConfigureAwait(false));
    }

    /// <summary>
    /// 取调用作用域内的仓储。
    /// </summary>
    protected static IRepository<TEntity, TKey> Repository<TEntity, TKey>(InvocationContext context)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services.GetRequiredService<IRepository<TEntity, TKey>>();
    }

    /// <summary>
    /// 在新调用内按标识查找。
    /// </summary>
    protected static async Task<Maybe<TEntity>> FindAsync<TEntity, TKey>(TestHost host, TKey id, string? tenantId = null)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        Maybe<TEntity> found = default;
        (await RunAsync(host, async context =>
        {
            found = await Repository<TEntity, TKey>(context).FindAsync(id, context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }, tenantId).ConfigureAwait(false)).ThrowIfFailure();
        return found;
    }

    /// <summary>
    /// 在新调用内按标识集合查找。
    /// </summary>
    protected static async Task<IReadOnlyList<TEntity>> FindManyAsync<TEntity, TKey>(TestHost host, IReadOnlyList<TKey> ids, string? tenantId = null)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        IReadOnlyList<TEntity> found = [];
        (await RunAsync(host, async context =>
        {
            found = await Repository<TEntity, TKey>(context).FindManyAsync(ids, context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }, tenantId).ConfigureAwait(false)).ThrowIfFailure();
        return found;
    }

    /// <summary>
    /// 把逐项结果折成一个：第一个失败，否则成功。
    /// </summary>
    protected static Result Fold(IReadOnlyList<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 在新调用内查询。
    /// </summary>
    protected static async Task<IReadOnlyList<TEntity>> ListAsync<TEntity, TKey>(TestHost host, EntityQuery<TEntity> query, string? tenantId = null)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        IReadOnlyList<TEntity> items = [];
        (await RunAsync(host, async context =>
        {
            items = await Repository<TEntity, TKey>(context).ListAsync(query, context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }, tenantId).ConfigureAwait(false)).ThrowIfFailure();
        return items;
    }

    /// <summary>
    /// 在新调用内计数。
    /// </summary>
    protected static async Task<long> CountAsync<TEntity, TKey>(TestHost host, EntityQuery<TEntity> query, string? tenantId = null)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        long count = -1;
        (await RunAsync(host, async context =>
        {
            count = await Repository<TEntity, TKey>(context).CountAsync(query, context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }, tenantId).ConfigureAwait(false)).ThrowIfFailure();
        return count;
    }
}
