// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

namespace XiHan.Framework.Persistence.Conformance;

/// <summary>
/// <c>persistence.relational@1</c> Provider 的一致性套件。继承并实现 <see cref="CreateFeature"/>，全部用例随之执行。
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
        var rejection = Error.Validation("CONFORMANCE_VALIDATION_001", "业务拒绝");
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
            throw new InvalidOperationException("处理器崩溃");
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
        (await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            for (var amount = 1; amount <= 5; amount++)
            {
                (await repository.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7(), Name = $"o{amount}", Amount = amount }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            }

            return Result.Success();
        }).ConfigureAwait(false)).ThrowIfFailure();

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
    /// 装配并启动宿主：被测特性、套件特性、三种套件实体的登记、套件时钟。
    /// </summary>
    protected Task<TestHost> StartAsync() => TestHost.StartAsync(
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
        });

    /// <summary>
    /// 在一次完整管道调用内执行 <paramref name="action"/>，其返回值作为调用结果；<paramref name="tenantId"/> 经 Baggage 进入 Tenant 阶段。
    /// </summary>
    protected static async Task<Result> RunAsync(TestHost host, Func<InvocationContext, Task<Result>> action, string? tenantId = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(action);
        var baggage = tenantId is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = tenantId };
        return await host.InvokeAsync(async context => context.SetOutcome(await action(context).ConfigureAwait(false)), baggage: baggage).ConfigureAwait(false);
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
