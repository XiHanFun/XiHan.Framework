// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
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
/// <c>persistence.relational@1</c> Provider 的一致性套件（含批量用例）
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateFeature"/>，全部用例随之执行。每个用例都以 <see cref="TestHost"/> 走完整管道：工作单元由 Transaction 阶段的中间件开启、提交或回滚
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class RepositoryConformance
{
    /// <summary>
    /// 套件时钟的起点
    /// </summary>
    public static DateTimeOffset Epoch { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 注入宿主的时钟
    /// </summary>
    /// <remarks>
    /// 审计字段的期望值据此计算
    /// </remarks>
    protected ConformanceClock Clock { get; } = new(Epoch);

    /// <summary>
    /// 宿主配置
    /// </summary>
    /// <remarks>
    /// Provider 需要的连接等在此提供
    /// </remarks>
    protected virtual IEnumerable<KeyValuePair<string, string?>> Configuration => [];

    /// <summary>
    /// 创建被测 Provider 的特性
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次
    /// </remarks>
    protected abstract IFeature CreateFeature();

    /// <summary>
    /// 附加服务注册
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// 附加应用配置
    /// </summary>
    protected virtual void ConfigureApp(AppBuilder builder)
    {
    }

    /// <summary>
    /// Provider 的直接写入入口
    /// </summary>
    /// <remarks>
    /// EF Core 与 SqlSugar 工作单元的 <c>EnsureTransactionAsync</c>（直接用客户端写入之前开启事务），参数是调用作用域里的 <see cref="IUnitOfWork"/>。为 <see langword="null"/>（缺省）表示 Provider 没有这个入口，用例 31 跳过
    /// </remarks>
    protected virtual Func<IUnitOfWork, CancellationToken, ValueTask>? EnsureTransaction => null;

    /// <summary>
    /// 套件可用的命名连接
    /// </summary>
    /// <remarks>
    /// 支持命名连接的 Provider 返回一个连接名，并在 <see cref="Configuration"/> 里声明它；SQLite 上要是另一个库文件（用例在业务事务占着默认库写锁时另开作用域写这个连接）。套件把 <see cref="ConformanceLease"/> 登记到这个连接，EF Core 的 DbContext 模型要包含它。为 <see langword="null"/>（缺省）表示不支持，用例 32 跳过
    /// </remarks>
    protected virtual string? NamedConnection => null;

    /// <summary>
    /// 用例 1：新增在同一工作单元内可读，提交后对新调用可见，并写入创建时间与并发戳
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
    /// 用例 2：未提交的写入对其他工作单元不可见
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
    /// 用例 3：标识重复，无论在同一工作单元还是跨工作单元，都返回 <see cref="PersistenceErrors.Duplicate"/>
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
    /// 用例 4：更新写入修改时间并刷新并发戳
    /// </summary>
    [Fact]
    public async Task Update_WritesModificationTime_AndRefreshesStamp()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id, Name = "v1" }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        // 只记录戳的值，更新后与新读出的实例比较
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
    /// 用例 5：更新或删除不存在的实体返回 <see cref="PersistenceErrors.NotFound"/>
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
    /// 用例 6：软删除实体被打标并隐藏，只有 <see cref="EntityQuery{T}.IncludeDeleted"/> 能看到
    /// </summary>
    /// <remarks>
    /// 之后更新与再次删除返回 NotFound
    /// </remarks>
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
    /// 用例 7：不实现软删除协议的实体被物理删除
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
    /// 用例 8：调用结果为失败时回滚，失败结果原样返回
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
    /// 用例 9：未处理异常回滚，并由内核转为 <see cref="KernelErrors.Unexpected"/>
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
    /// 用例 10：多租户实体没有当前租户时拒绝新增
    /// </summary>
    /// <remarks>
    /// 不同租户互不可见；无租户的调用看不到任何多租户实体
    /// </remarks>
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
    /// 用例 11：查询按过滤、排序、分页返回
    /// </summary>
    /// <remarks>
    /// 计数忽略分页
    /// </remarks>
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
    /// 用例 11b
    /// </summary>
    /// <remarks>
    /// 规约里可空值类型与 null 的相等 / 不等按存在性（<c>IS NULL</c> / <c>IS NOT NULL</c>）过滤——字面量 null、闭包里的 null 变量、左右互换、与 <c>HasValue</c> 都一致，组合到别的条件里也成立。SqlSugar 原本把 <c>DateTimeOffset?</c> 的提升比较译成 <c>= NULL</c>（永假、静默空集），Provider 必须按本用例的语义翻译
    /// </remarks>
    [Fact]
    public async Task List_NullableValueTypeComparedWithNull_FiltersByPresence()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var touched = Guid.CreateVersion7();
        (await RunAsync(host, async context => Fold(await Repository<ConformanceOrder, Guid>(context).AddRangeAsync(
            [new ConformanceOrder { Id = touched, Name = "touched", Amount = 1 }, new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "untouched", Amount = 2 }],
            context.CancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ThrowIfFailure();
        Clock.Advance(TimeSpan.FromMinutes(1));
        // 更新 touched 写入 ModifiedAt；untouched 的 ModifiedAt 仍是 null
        (await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            var order = (await repository.FindAsync(touched, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            order.Amount = 3;
            return await repository.UpdateAsync(order, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false)).ThrowIfFailure();

        DateTimeOffset? none = null;
        Assert.Equal(["untouched"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.ModifiedAt == null)).ConfigureAwait(false));
        Assert.Equal(["touched"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.ModifiedAt != null)).ConfigureAwait(false));
        Assert.Equal(["untouched"], await NamesAsync(new Specification<ConformanceOrder>(static o => null == o.ModifiedAt)).ConfigureAwait(false));
        Assert.Equal(["untouched"], await NamesAsync(new Specification<ConformanceOrder>(o => o.ModifiedAt == none)).ConfigureAwait(false));
        Assert.Equal(["touched"], await NamesAsync(new Specification<ConformanceOrder>(o => o.ModifiedAt != none)).ConfigureAwait(false));
        Assert.Equal(["untouched"], await NamesAsync(new Specification<ConformanceOrder>(static o => !o.ModifiedAt.HasValue)).ConfigureAwait(false));
        Assert.Equal(["untouched", "touched"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.ModifiedAt == null || o.Amount == 3)).ConfigureAwait(false));
        Assert.Equal(["touched"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.ModifiedAt != null && o.Amount == 3)).ConfigureAwait(false));

        // 按规约过滤后的订单名，按金额升序
        async Task<List<string>> NamesAsync(Specification<ConformanceOrder> filter)
            => (await ListAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>().Where(filter).OrderBy(static o => o.Amount)).ConfigureAwait(false)).Select(static o => o.Name).ToList();
    }

    /// <summary>
    /// 用例 11c：可空值类型判空、取反与 <c>&amp;&amp;</c> / <c>||</c> 嵌套组合照常过滤
    /// </summary>
    /// <remarks>
    /// 或条件、取反出现在与条件的右侧，判空或取反是或条件的左操作数（发件箱的到期判定就是 <c>A &amp;&amp; B &amp;&amp; (可空值 == null || …)</c>）。SqlSugar 在这些位置会漏写 <c>AND</c> / <c>OR</c>，生成语法错误的 SQL，Provider 必须照常翻译。规约只用存在性判定与整数比较，不对可空值做取反后的大小比较（C# 与 SQL 对 null 的三值逻辑不同）
    /// </remarks>
    [Fact]
    public async Task List_NullChecksAndNegationsNestedInAndOr_FilterAsWritten()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var touched = Guid.CreateVersion7();
        var late = Guid.CreateVersion7();
        (await RunAsync(host, async context => Fold(await Repository<ConformanceOrder, Guid>(context).AddRangeAsync(
            [
                new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "fresh", Amount = 1 },
                new ConformanceOrder { Id = touched, Name = "touched", Amount = 2 },
                new ConformanceOrder { Id = late, Name = "late", Amount = 3 },
            ],
            context.CancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ThrowIfFailure();
        Clock.Advance(TimeSpan.FromMinutes(1));
        // touched 与 late 写入 ModifiedAt；fresh 的 ModifiedAt 仍是 null，三条的 DeletedAt 都是 null
        (await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            return Fold(await repository.UpdateRangeAsync(await repository.FindManyAsync([touched, late], context.CancellationToken).ConfigureAwait(false), context.CancellationToken).ConfigureAwait(false));
        }).ConfigureAwait(false)).ThrowIfFailure();

        Assert.Equal(["fresh", "touched"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.DeletedAt == null && o.Amount >= 1 && (o.ModifiedAt == null || o.Amount <= 2))).ConfigureAwait(false));
        Assert.Equal(["fresh", "touched"], await NamesAsync(new Specification<ConformanceOrder>(static o => (o.ModifiedAt == null || o.Amount <= 2) && o.DeletedAt == null && o.Amount >= 1)).ConfigureAwait(false));
        Assert.Equal(["fresh", "late"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.Amount >= 1 && (!o.ModifiedAt.HasValue || o.Amount == 3))).ConfigureAwait(false));
        Assert.Equal(["fresh", "late"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.Amount >= 1 && (!(o.Amount == 2) || o.ModifiedAt == null))).ConfigureAwait(false));
        Assert.Equal(["fresh", "late"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.Amount >= 1 && !(o.ModifiedAt != null && o.Amount == 2))).ConfigureAwait(false));
        Assert.Equal(["fresh", "touched"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.DeletedAt == null && (o.Amount == 2 || (o.ModifiedAt == null && o.Amount == 1)))).ConfigureAwait(false));
        Assert.Equal(["touched"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.DeletedAt == null && !(o.ModifiedAt == null || o.Amount == 3))).ConfigureAwait(false));
        Assert.Equal(["touched", "late"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.Amount >= 1 && (o.ModifiedAt != null || o.DeletedAt != null))).ConfigureAwait(false));
        Assert.Equal(["fresh"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.Amount >= 1 && (o.ModifiedAt == null || o.DeletedAt != null))).ConfigureAwait(false));

        // 按规约过滤后的订单名，按金额升序
        async Task<List<string>> NamesAsync(Specification<ConformanceOrder> filter)
            => (await ListAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>().Where(filter).OrderBy(static o => o.Amount)).ConfigureAwait(false)).Select(static o => o.Name).ToList();
    }

    /// <summary>
    /// 用例 11d：一次查询最多取 <see cref="EntityQuery.DefaultMaxTake"/> 条
    /// </summary>
    /// <remarks>
    /// 比上限多一条数据：缺省查询与取满上限的查询都只返回上限条，计数仍是全部，第二页取到剩下的一条。超过上限的取出条数在构造查询时就以 <see cref="PersistenceErrors.TakeExceeded"/> 拒绝，不交给 Provider 截断；调用里先写入、再用超限的条数构造查询，调用以该码的校验结果结束，写入回滚
    /// </remarks>
    [Fact]
    public async Task List_ReturnsAtMostTheMaximumTake_AndLargerTakesAreRejected()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        (await RunAsync(host, async context => Fold(await Repository<ConformanceItem, int>(context).AddRangeAsync(
            [.. Enumerable.Range(1, EntityQuery.DefaultMaxTake + 1).Select(static id => new ConformanceItem { Id = id, Label = "item" })],
            context.CancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ThrowIfFailure();

        var ordered = EntityQuery.All<ConformanceItem>().OrderBy(static i => i.Id);
        Assert.Equal(EntityQuery.DefaultMaxTake, (await ListAsync<ConformanceItem, int>(host, ordered).ConfigureAwait(false)).Count);
        Assert.Equal(EntityQuery.DefaultMaxTake, (await ListAsync<ConformanceItem, int>(host, ordered.Page(0, EntityQuery.DefaultMaxTake)).ConfigureAwait(false)).Count);
        Assert.Equal([EntityQuery.DefaultMaxTake + 1], (await ListAsync<ConformanceItem, int>(host, ordered.Page(EntityQuery.DefaultMaxTake, EntityQuery.DefaultMaxTake)).ConfigureAwait(false)).Select(static i => i.Id));
        Assert.Equal(EntityQuery.DefaultMaxTake + 1, await CountAsync<ConformanceItem, int>(host, ordered).ConfigureAwait(false));
        Assert.Equal(PersistenceErrors.TakeExceeded, Assert.ThrowsAny<FrameworkException>(() => ordered.Page(0, EntityQuery.DefaultMaxTake + 1)).Code);

        var written = Guid.CreateVersion7();
        var rejected = await RunAsync(host, async context =>
        {
            (await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = written, Name = "written" }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            // 页大小来自调用方且没有在请求契约上限定：构造查询时就被拒绝，不会走到 Provider
            _ = await Repository<ConformanceItem, int>(context).ListAsync(ordered.Page(0, EntityQuery.DefaultMaxTake * 5), context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.Equal(PersistenceErrors.TakeExceeded, rejected.Error?.Code);
        Assert.Equal("VALIDATION", rejected.Error?.Kind);
        Assert.Equal(EntityQuery.DefaultMaxTake * 5, rejected.Error?.Details?["take"]);
        Assert.Equal(EntityQuery.DefaultMaxTake, rejected.Error?.Details?["max"]);
        Assert.False((await FindAsync<ConformanceOrder, Guid>(host, written).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 11e：规约里字符串的包含、前缀与后缀按字面匹配
    /// </summary>
    /// <remarks>
    /// 实参里的 <c>%</c>、<c>_</c>、<c>[</c>、反斜杠、<c>!</c> 与单引号不是通配符、转义符或语法：<c>t_2</c> 不命中 <c>budgetx2026</c>，<c>0%</c> 不命中 <c>500 off</c>，<c>[a]</c> 不命中 <c>a</c>（SQL Server 的字符类），<c>a\b</c> 不命中 <c>ab</c>（PostgreSQL 与 MySQL 的 <c>LIKE</c> 缺省以反斜杠转义），<c>!y</c> 不命中 <c>xy</c>。实参是闭包变量与常量都一样。大小写是否敏感各库不同，不在本用例：数据与实参都是小写（RFC-0005「规约里的字符串匹配」）。SqlSugar 原本把它们译成不转义的 <c>LIKE</c>，Provider 必须按本用例的语义翻译
    /// </remarks>
    [Fact]
    public async Task List_StringContainsStartsWithEndsWith_MatchWildcardCharactersLiterally()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        string[] names = ["budget_2026", "budgetx2026", "50% off", "500 off", "a\\b", "ab", "[a]b", "a", "x!y", "xy", "it's", "its"];
        (await RunAsync(host, async context => Fold(await Repository<ConformanceOrder, Guid>(context).AddRangeAsync(
            [.. names.Select(static (name, index) => new ConformanceOrder { Id = Guid.CreateVersion7(), Name = name, Amount = index })],
            context.CancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ThrowIfFailure();

        (string Keyword, string[] Expected)[] contains =
        [
            ("t_2", ["budget_2026"]),
            ("0%", ["50% off"]),
            ("_", ["budget_2026"]),
            ("[a]", ["[a]b"]),
            ("\\", ["a\\b"]),
            ("a\\b", ["a\\b"]),
            ("!y", ["x!y"]),
            ("'s", ["it's"]),
            ("' or '1'='1", []),
        ];
        (string Keyword, string[] Expected)[] prefixes = [("50%", ["50% off"]), ("[a", ["[a]b"]), ("a\\", ["a\\b"]), ("x!", ["x!y"])];
        (string Keyword, string[] Expected)[] suffixes = [("_2026", ["budget_2026"]), ("% off", ["50% off"]), ("\\b", ["a\\b"]), ("!y", ["x!y"])];

        // 整组断言：失败时一次列出全部不符的实参
        var actual = new List<string>();
        foreach (var (keyword, _) in contains)
        {
            actual.Add(Describe("contains", keyword, await NamesAsync(new Specification<ConformanceOrder>(o => o.Name.Contains(keyword))).ConfigureAwait(false)));
        }

        foreach (var (keyword, _) in prefixes)
        {
            actual.Add(Describe("starts", keyword, await NamesAsync(new Specification<ConformanceOrder>(o => o.Name.StartsWith(keyword))).ConfigureAwait(false)));
        }

        foreach (var (keyword, _) in suffixes)
        {
            actual.Add(Describe("ends", keyword, await NamesAsync(new Specification<ConformanceOrder>(o => o.Name.EndsWith(keyword))).ConfigureAwait(false)));
        }

        Assert.Equal(
            [
                .. contains.Select(static c => Describe("contains", c.Keyword, c.Expected)),
                .. prefixes.Select(static c => Describe("starts", c.Keyword, c.Expected)),
                .. suffixes.Select(static c => Describe("ends", c.Keyword, c.Expected)),
            ],
            actual);
        Assert.Equal(["budget_2026"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.Name.Contains("t_2"))).ConfigureAwait(false));
        Assert.Equal(["50% off"], await NamesAsync(new Specification<ConformanceOrder>(static o => o.Name.StartsWith("50%"))).ConfigureAwait(false));
        Assert.Equal(1, await CountAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>().Where(new Specification<ConformanceOrder>(static o => o.Name.EndsWith("_2026")))).ConfigureAwait(false));

        // 按规约过滤后的订单名，按金额（写入顺序）升序
        async Task<List<string>> NamesAsync(Specification<ConformanceOrder> filter)
            => (await ListAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>().Where(filter).OrderBy(static o => o.Amount)).ConfigureAwait(false)).Select(static o => o.Name).ToList();

        // 一个实参的可读描述：方法、实参与命中的名字
        static string Describe(string method, string keyword, IEnumerable<string> found) => $"{method} '{keyword}' -> [{string.Join(" | ", found)}]";
    }

    /// <summary>
    /// 用例 11f：规约里字符串的包含、前缀与后缀在大小写上一致
    /// </summary>
    /// <remarks>
    /// 同一个 Provider 上，三个方法之间、实参是闭包变量、常量还是另一列之间，ASCII 字母的大小写规则相同：全都区分或全都不区分，不断言是哪一种（照各库，RFC-0005「规约里的字符串匹配」）。列实参取多租户实体的租户列：租户 <c>acme</c> 的笔记 <c>ACME</c> 与它只差大小写。EF Core 的 SQLite Provider 原本把 <c>Contains</c> 译成区分大小写的 <c>instr</c>、<c>StartsWith</c> / <c>EndsWith</c> 的值实参译成不区分的 <c>LIKE</c>、列实参译成区分的 <c>substr</c> 比较，一个库上三种规则，Provider 必须统一成一套
    /// </remarks>
    [Fact]
    public async Task List_StringContainsStartsWithEndsWith_AgreeOnLetterCase()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        const string tenant = "acme";
        (await RunAsync(host, async context => Fold(await Repository<ConformanceNote, Guid>(context).AddRangeAsync(
            [new ConformanceNote { Id = Guid.CreateVersion7(), Text = "ACME" }, new ConformanceNote { Id = Guid.CreateVersion7(), Text = "other" }],
            context.CancellationToken).ConfigureAwait(false)), tenant).ConfigureAwait(false)).ThrowIfFailure();

        var keyword = "acme";
        (string Form, Specification<ConformanceNote> Filter)[] forms =
        [
            ("contains variable", new Specification<ConformanceNote>(n => n.Text.Contains(keyword))),
            ("contains constant", new Specification<ConformanceNote>(static n => n.Text.Contains("acme"))),
            ("contains column", new Specification<ConformanceNote>(static n => n.Text.Contains(n.TenantId))),
            ("starts variable", new Specification<ConformanceNote>(n => n.Text.StartsWith(keyword))),
            ("starts constant", new Specification<ConformanceNote>(static n => n.Text.StartsWith("acme"))),
            ("starts column", new Specification<ConformanceNote>(static n => n.Text.StartsWith(n.TenantId))),
            ("ends variable", new Specification<ConformanceNote>(n => n.Text.EndsWith(keyword))),
            ("ends constant", new Specification<ConformanceNote>(static n => n.Text.EndsWith("acme"))),
            ("ends column", new Specification<ConformanceNote>(static n => n.Text.EndsWith(n.TenantId))),
        ];

        // 九种写法各自命中的笔记：全部相同才算一致，失败时一次列出各写法的结果
        var outcomes = new List<(string Form, string Found)>();
        foreach (var (form, filter) in forms)
        {
            var found = await ListAsync<ConformanceNote, Guid>(host, EntityQuery.All<ConformanceNote>().Where(filter), tenant).ConfigureAwait(false);
            outcomes.Add((form, $"[{string.Join(" | ", found.Select(static n => n.Text))}]"));
        }

        Assert.True(
            outcomes.Select(static o => o.Found).Distinct(StringComparer.Ordinal).Count() == 1,
            "Contains, StartsWith and EndsWith must follow one letter-case rule for variables, constants and columns: " + string.Join("; ", outcomes.Select(static o => $"{o.Form} -> {o.Found}")) + ".");
        // 大小写相同的总是命中，另一条笔记哪种写法都不命中
        var exact = await ListAsync<ConformanceNote, Guid>(host, EntityQuery.All<ConformanceNote>().Where(new Specification<ConformanceNote>(static n => n.Text.Contains("ACME"))), tenant).ConfigureAwait(false);
        Assert.Equal(["ACME"], exact.Select(static n => n.Text));
        Assert.DoesNotContain("other", outcomes[0].Found, StringComparison.Ordinal);
    }

    /// <summary>
    /// 用例 12：在 Transaction 阶段之外使用仓储抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>
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
    /// 用例 13：Provider 注册 <see cref="IIdGenerator{TKey}"/>（Guid 版本 7）
    /// </summary>
    /// <remarks>
    /// 并在清单里声明 <c>persistence.relational@1</c>
    /// </remarks>
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
    /// 用例 14：批量新增一次写入、读己之写，结果与入参一一对应
    /// </summary>
    /// <remarks>
    /// 批内重复与跨工作单元的重复标识按项报告，其余项照常落库；按标识集合查找只返回存在的，顺序按入参、重复只一次
    /// </remarks>
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
    /// 用例 15：批量更新一次写入
    /// </summary>
    /// <remarks>
    /// 写入修改时间并刷新并发戳；并发戳过期的游离实例返回 <see cref="PersistenceErrors.Concurrency"/>、不存在的返回 NotFound，其余项照常生效
    /// </remarks>
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
    /// 用例 16：批量删除一次写入
    /// </summary>
    /// <remarks>
    /// 软删除实体打标隐藏、普通实体物理删除；批内同一实体第二次与不存在的实体按项返回 NotFound，其余项照常生效
    /// </remarks>
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
    /// 用例 17：空集合不触后端、返回空列表
    /// </summary>
    /// <remarks>
    /// 在 Transaction 阶段之外使用批量方法同样抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>
    /// </remarks>
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
    /// 装配并启动宿主：被测特性、套件特性、三种套件实体的登记、套件时钟
    /// </summary>
    /// <remarks>
    /// Provider 给出 <see cref="NamedConnection"/> 时另把 <see cref="ConformanceLease"/> 登记到那个连接
    /// </remarks>
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
            if (NamedConnection is { } connection)
            {
                services.AddRepository<ConformanceLease, Guid>(connection);
            }

            ConfigureServices(services);
            extra?.Invoke(services);
        });

    /// <summary>
    /// 在一次完整管道调用内执行 <paramref name="action"/>，其返回值作为调用结果
    /// </summary>
    /// <remarks>
    /// <paramref name="tenantId"/> 经 Baggage 进入 Tenant 阶段
    /// </remarks>
    protected static async Task<Result> RunAsync(TestHost host, Func<InvocationContext, Task<Result>> action, string? tenantId = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(action);
        // 租户不直接写安全上下文，而是放进 Baggage 由 ConformanceFeature 的 Tenant 阶段中间件解析，与真实调用路径一致
        var baggage = tenantId is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = tenantId };
        return await host.InvokeAsync(async context => context.SetOutcome(await action(context).ConfigureAwait(false)), baggage: baggage).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 19：时间范围提示不是过滤条件
    /// </summary>
    /// <remarks>
    /// 不分表的实体带 <c>Within</c> 查询仍返回范围之外的行
    /// 计数也不变。范围颠倒是编程错误
    /// </remarks>
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
    /// 用例 20：聚合记录的领域事件在提交时交给派发器
    /// </summary>
    /// <remarks>
    /// 新增与更新过的聚合都登记、同一实例一次、带当前租户。派发后缓冲清空。失败的调用回滚，不派发
    /// </remarks>
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
    /// 用例 21：派发器失败即回滚且调用结果是派发器的错误
    /// </summary>
    /// <remarks>
    /// 没有注册派发器却有事件返回 <see cref="PersistenceErrors.EventsUndispatched"/> 并回滚；没有事件的调用不受影响
    /// </remarks>
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
    /// 用例 22：更新沿用存储的创建时间与删除标记，删除也不改创建时间
    /// </summary>
    /// <remarks>
    /// 自己构造的游离实例与读出后改过的实例两种：调用方实例上的创建时间、删除标记一律不生效，写入成功后调用方实例拿到存储里的值；软删除只打标，创建时间仍是新增时的值
    /// </remarks>
    [Fact]
    public async Task Update_And_Remove_KeepStoredCreationTimeAndDeletionMarker()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id, Name = "v1" }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        var stamp = (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow().ConcurrencyStamp;
        Clock.Advance(TimeSpan.FromHours(1));

        // 游离实例：并发戳对得上，创建时间与删除标记是调用方编的
        var detached = new ConformanceOrder { Id = id, Name = "v2", ConcurrencyStamp = stamp, CreatedAt = Epoch.AddYears(3), IsDeleted = true, DeletedAt = Epoch.AddYears(3) };
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).UpdateAsync(detached, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        Assert.Equal(Epoch, detached.CreatedAt);
        Assert.False(detached.IsDeleted);
        Assert.Null(detached.DeletedAt);
        var afterDetached = (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("v2", afterDetached.Name);
        Assert.Equal(Epoch, afterDetached.CreatedAt);
        Assert.Equal(Clock.GetUtcNow(), afterDetached.ModifiedAt);

        // 读出后改过的实例
        Clock.Advance(TimeSpan.FromHours(1));
        (await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            var order = (await repository.FindAsync(id, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            order.Name = "v3";
            order.CreatedAt = Epoch.AddDays(-5);
            order.IsDeleted = true;
            return await repository.UpdateAsync(order, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false)).ThrowIfFailure();
        var afterModified = (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("v3", afterModified.Name);
        Assert.Equal(Epoch, afterModified.CreatedAt);
        Assert.False(afterModified.IsDeleted);

        // 删除前改了创建时间：墓碑上仍是新增时的值
        Clock.Advance(TimeSpan.FromHours(1));
        (await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            var order = (await repository.FindAsync(id, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            order.CreatedAt = Epoch.AddDays(-5);
            return await repository.RemoveAsync(order, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false)).ThrowIfFailure();
        var tombstone = Assert.Single(await ListAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>() with { IncludeDeleted = true }).ConfigureAwait(false));
        Assert.True(tombstone.IsDeleted);
        Assert.Equal(Clock.GetUtcNow(), tombstone.DeletedAt);
        Assert.Equal(Epoch, tombstone.CreatedAt);
    }

    /// <summary>
    /// 用例 23：更新不能把实体改到别的租户
    /// </summary>
    /// <remarks>
    /// 游离实例带着别的租户、读出后改了租户，两种更新都成功，但行仍属于新增时的租户，别的租户看不到它；调用方实例拿回存储里的租户
    /// </remarks>
    [Fact]
    public async Task Update_KeepsStoredTenant()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceNote, Guid>(context).AddAsync(new ConformanceNote { Id = id, Text = "hi" }, context.CancellationToken), tenantId: "t1").ConfigureAwait(false)).ThrowIfFailure();

        var detached = new ConformanceNote { Id = id, TenantId = "t2", Text = "moved" };
        (await RunAsync(host, context => Repository<ConformanceNote, Guid>(context).UpdateAsync(detached, context.CancellationToken), tenantId: "t1").ConfigureAwait(false)).ThrowIfFailure();
        Assert.Equal("t1", detached.TenantId);
        var afterDetached = (await FindAsync<ConformanceNote, Guid>(host, id, tenantId: "t1").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("moved", afterDetached.Text);
        Assert.Equal("t1", afterDetached.TenantId);
        Assert.False((await FindAsync<ConformanceNote, Guid>(host, id, tenantId: "t2").ConfigureAwait(false)).HasValue);

        (await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceNote, Guid>(context);
            var note = (await repository.FindAsync(id, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            note.TenantId = "t2";
            note.Text = "again";
            return await repository.UpdateAsync(note, context.CancellationToken).ConfigureAwait(false);
        }, tenantId: "t1").ConfigureAwait(false)).ThrowIfFailure();
        Assert.Equal("again", (await FindAsync<ConformanceNote, Guid>(host, id, tenantId: "t1").ConfigureAwait(false)).ValueOrThrow().Text);
        Assert.False((await FindAsync<ConformanceNote, Guid>(host, id, tenantId: "t2").ConfigureAwait(false)).HasValue);
        Assert.Equal(0, await CountAsync<ConformanceNote, Guid>(host, EntityQuery.All<ConformanceNote>(), tenantId: "t2").ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 24：两个工作单元先后新增同一标识，检查时对方尚未提交
    /// </summary>
    /// <remarks>
    /// 只有一个成功，另一个以 <see cref="PersistenceErrors.Duplicate"/> 结束，留下的是成功者的行，不会静默覆盖。第一个调用新增后停在提交之前，第二个调用此时开始：关系型 Provider 里第二个调用的写入要等第一个提交后才失败，所以它跑在另一个线程上；内存实现里谁先提交谁成功，后提交的在提交时失败。断言不依赖哪一个先完成
    /// </remarks>
    [Fact]
    public async Task ConcurrentAdds_OfSameId_OneSucceeds_OtherReturnsDuplicate()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        var firstAdded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = RunAsync(host, async context =>
        {
            var added = await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id, Name = "first" }, context.CancellationToken).ConfigureAwait(false);
            firstAdded.SetResult();
            await releaseFirst.Task.ConfigureAwait(false);
            return added;
        });
        await firstAdded.Task.ConfigureAwait(false);
        var second = Task.Run(() => RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id, Name = "second" }, context.CancellationToken)));
        releaseFirst.SetResult();
        var results = await Task.WhenAll(first, second).ConfigureAwait(false);

        Assert.Single(results, static r => r.IsSuccess);
        Assert.Equal(PersistenceErrors.Duplicate, Assert.Single(results, static r => r.IsFailure).Error?.Code);
        var winner = results[0].IsSuccess ? "first" : "second";
        Assert.Equal(winner, (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow().Name);
    }

    /// <summary>
    /// 用例 25：回滚的调用不在存储里留下任何痕迹
    /// </summary>
    /// <remarks>
    /// 读出的实例改名后更新、另一个读出的实例删除，调用以失败结束：存储里的名称、并发戳、修改时间与删除标记都是调用前的值。更新之后、提交之前，另一个工作单元读到的也是旧值
    /// </remarks>
    [Fact]
    public async Task RolledBackWrites_LeaveStoredEntitiesUntouched()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var renamed = Guid.CreateVersion7();
        var removed = Guid.CreateVersion7();
        (await RunAsync(host, async context => Fold(await Repository<ConformanceOrder, Guid>(context).AddRangeAsync(
            [new ConformanceOrder { Id = renamed, Name = "a" }, new ConformanceOrder { Id = removed, Name = "b" }],
            context.CancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ThrowIfFailure();
        var stamps = (await FindManyAsync<ConformanceOrder, Guid>(host, [renamed, removed]).ConfigureAwait(false)).Select(static o => o.ConcurrencyStamp).ToList();
        Clock.Advance(TimeSpan.FromHours(1));
        var rejection = Error.Literal("CONFORMANCE_VALIDATION_001", "Rejected by the business rule.");
        Maybe<ConformanceOrder> seenByOther = default;

        var outcome = await RunAsync(host, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            var order = (await repository.FindAsync(renamed, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            order.Name = "a2";
            (await repository.UpdateAsync(order, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            var doomed = (await repository.FindAsync(removed, context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();
            (await repository.RemoveAsync(doomed, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();

            // 尚未提交时另一个工作单元读同一行
            await using var other = context.Services.CreateAsyncScope();
            var otherUnitOfWork = other.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await otherUnitOfWork.BeginAsync(UnitOfWorkOptions.Default, context.CancellationToken).ConfigureAwait(false);
            seenByOther = await other.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>().FindAsync(renamed, context.CancellationToken).ConfigureAwait(false);
            await otherUnitOfWork.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            return rejection;
        }).ConfigureAwait(false);

        Assert.Equal(rejection, outcome.Error);
        Assert.Equal("a", seenByOther.ValueOrThrow().Name);
        Assert.Equal(stamps[0], seenByOther.ValueOrThrow().ConcurrencyStamp);
        var current = await FindManyAsync<ConformanceOrder, Guid>(host, [renamed, removed]).ConfigureAwait(false);
        Assert.Equal(["a", "b"], current.Select(static o => o.Name));
        Assert.Equal(stamps, current.Select(static o => o.ConcurrencyStamp));
        Assert.All(current, static o =>
        {
            Assert.Null(o.ModifiedAt);
            Assert.False(o.IsDeleted);
            Assert.Null(o.DeletedAt);
        });
    }

    /// <summary>
    /// 用例 26：只读操作里的写入都被拒绝，读照常
    /// </summary>
    /// <remarks>
    /// 在动词为 <c>get</c> 的操作里：查找与计数照常；单条与批量的新增、更新、删除都返回 <see cref="PersistenceErrors.ReadOnly"/>，空批量仍返回空列表。处理器忽略全部拒绝而返回成功时调用仍以它结束，把拒绝作为结果返回时同样以它结束；存储不变
    /// </remarks>
    [Fact]
    public async Task ReadOnlyOperation_RejectsEveryWrite_AndReadsAsUsual()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var existing = Guid.CreateVersion7();
        var fresh = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = existing, Name = "existing", Amount = 1 }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        var stored = (await FindAsync<ConformanceOrder, Guid>(host, existing).ConfigureAwait(false)).ValueOrThrow();
        var codes = new List<string?>();
        var found = false;
        long count = -1;
        var emptyBatch = -1;

        var ignored = await InvokeScriptAsync(host, ReadScriptOperation.Id, async context =>
        {
            var repository = Repository<ConformanceOrder, Guid>(context);
            var cancellationToken = context.CancellationToken;
            found = (await repository.FindAsync(existing, cancellationToken).ConfigureAwait(false)).HasValue;
            count = await repository.CountAsync(EntityQuery.All<ConformanceOrder>(), cancellationToken).ConfigureAwait(false);
            stored.Name = "changed";
            codes.Add((await repository.AddAsync(new ConformanceOrder { Id = fresh, Name = "fresh" }, cancellationToken).ConfigureAwait(false)).Error?.Code);
            codes.Add((await repository.UpdateAsync(stored, cancellationToken).ConfigureAwait(false)).Error?.Code);
            codes.Add((await repository.RemoveAsync(stored, cancellationToken).ConfigureAwait(false)).Error?.Code);
            codes.AddRange((await repository.AddRangeAsync([new ConformanceOrder { Id = Guid.CreateVersion7() }, new ConformanceOrder { Id = Guid.CreateVersion7() }], cancellationToken).ConfigureAwait(false)).Select(static r => r.Error?.Code));
            codes.AddRange((await repository.UpdateRangeAsync([stored], cancellationToken).ConfigureAwait(false)).Select(static r => r.Error?.Code));
            codes.AddRange((await repository.RemoveRangeAsync([stored], cancellationToken).ConfigureAwait(false)).Select(static r => r.Error?.Code));
            emptyBatch = (await repository.AddRangeAsync([], cancellationToken).ConfigureAwait(false)).Count;

            // 忽略全部拒绝：工作单元已记下失败，提交返回同一错误
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(found);
        Assert.Equal(1, count);
        Assert.Equal(Enumerable.Repeat<string?>(PersistenceErrors.ReadOnly, 7), codes);
        Assert.Equal(0, emptyBatch);
        Assert.Equal(PersistenceErrors.ReadOnly, ignored.Error?.Code);

        var propagated = await InvokeScriptAsync(host, ReadScriptOperation.Id, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = fresh }, context.CancellationToken)).ConfigureAwait(false);
        Assert.Equal(PersistenceErrors.ReadOnly, propagated.Error?.Code);

        Assert.Equal("existing", (await FindAsync<ConformanceOrder, Guid>(host, existing).ConfigureAwait(false)).ValueOrThrow().Name);
        Assert.False((await FindAsync<ConformanceOrder, Guid>(host, fresh).ConfigureAwait(false)).HasValue);
        Assert.Equal(1, await CountAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>()).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 27：只读操作在新的服务作用域里调用写操作，写入照常提交
    /// </summary>
    /// <remarks>
    /// 只读不向内传播：内层写操作开自己的可写工作单元、成功即提交；外层自己的写入仍被拒绝、调用以 <see cref="PersistenceErrors.ReadOnly"/> 结束，已提交的内层写入不随外层回滚
    /// </remarks>
    [Fact]
    public async Task WriteOperation_CalledFromReadOnlyOperation_InItsOwnScope_Commits()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var scripts = host.Services.GetRequiredService<ConformanceScripts>();
        var inner = Guid.CreateVersion7();
        var outer = Guid.CreateVersion7();
        Result<ConformanceScriptResponse> nested = default;
        Result outerWrite = default;

        var result = await InvokeScriptAsync(host, ReadScriptOperation.Id, async context =>
        {
            // 嵌套调用新建服务作用域与上下文（沿用 Baggage），内层得到自己的工作单元
            await using var scope = context.Services.CreateAsyncScope();
            var call = App.CreateContext(scope.ServiceProvider, new InvocationContextOptions { Baggage = new Dictionary<string, string>(context.Baggage, StringComparer.Ordinal) }, context.CancellationToken);
            var key = scripts.Add(c => Repository<ConformanceOrder, Guid>(c).AddAsync(new ConformanceOrder { Id = inner, Name = "inner" }, c.CancellationToken));
            nested = await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync<ConformanceScriptRequest, ConformanceScriptResponse>(call, WriteScriptOperation.Id, new ConformanceScriptRequest(key)).ConfigureAwait(false);
            outerWrite = await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = outer, Name = "outer" }, context.CancellationToken).ConfigureAwait(false);
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(nested.IsSuccess, nested.ToString());
        Assert.Equal(PersistenceErrors.ReadOnly, outerWrite.Error?.Code);
        Assert.Equal(PersistenceErrors.ReadOnly, result.Error?.Code);
        Assert.Equal("inner", (await FindAsync<ConformanceOrder, Guid>(host, inner).ConfigureAwait(false)).ValueOrThrow().Name);
        Assert.False((await FindAsync<ConformanceOrder, Guid>(host, outer).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 28：同一服务作用域里再经管道的调用被拒绝
    /// </summary>
    /// <remarks>
    /// 内层以 <see cref="PersistenceErrors.InvalidUnitOfWorkState"/> 结束，处理器不执行。作用域里的工作单元已由外层开始：内层不加入它、也不再开始它；外层的工作单元不受影响，照常提交自己的写入
    /// </remarks>
    [Fact]
    public async Task NestedInvocation_InTheSameScope_EndsWithInvalidState_WithoutRunningTheHandler()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var scripts = host.Services.GetRequiredService<ConformanceScripts>();
        var id = Guid.CreateVersion7();
        var ran = false;
        Result<ConformanceScriptResponse> nested = default;

        var result = await InvokeScriptAsync(host, WriteScriptOperation.Id, async context =>
        {
            // 以外层的服务作用域建上下文：作用域里只有一个工作单元，且已经开始
            var call = App.CreateContext(context.Services, new InvocationContextOptions { Baggage = new Dictionary<string, string>(context.Baggage, StringComparer.Ordinal) }, context.CancellationToken);
            var key = scripts.Add(_ =>
            {
                ran = true;
                return Task.FromResult(Result.Success());
            });
            nested = await context.Services.GetRequiredService<IOperationInvoker>().InvokeAsync<ConformanceScriptRequest, ConformanceScriptResponse>(call, ReadScriptOperation.Id, new ConformanceScriptRequest(key)).ConfigureAwait(false);
            return await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id, Name = "outer" }, context.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.False(ran);
        Assert.Equal(PersistenceErrors.InvalidUnitOfWorkState, nested.Error?.Code);
        Assert.Equal("outer", (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow().Name);
    }

    /// <summary>
    /// 用例 29：数据库事务在第一次写入时开启，提交与回滚后复位
    /// </summary>
    /// <remarks>
    /// <see cref="IUnitOfWork.HasTransaction"/>：开始前与刚开始时为假；查找、按标识集合查找、查询、计数与空批量之后仍为假；第一次写入之后为真，再写入仍为真；提交之后为假。失败结果、未处理异常、管道外的提交与回滚之后为假。结果失败的写入（更新不存在的实体）同样开启。管道外的值在作用域释放之前核对
    /// </remarks>
    [Fact]
    public async Task HasTransaction_OpensOnTheFirstWrite_AndResetsAfterCommitAndRollback()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var existing = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = existing, Name = "existing" }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        var observed = new List<(string Step, bool Open)>();
        IUnitOfWork? committed = null;

        var result = await RunAsync(host, async context =>
        {
            var unitOfWork = committed = context.Services.GetRequiredService<IUnitOfWork>();
            var repository = Repository<ConformanceOrder, Guid>(context);
            var cancellationToken = context.CancellationToken;
            observed.Add(("begun", unitOfWork.HasTransaction));
            _ = await repository.FindAsync(existing, cancellationToken).ConfigureAwait(false);
            _ = await repository.FindManyAsync([existing], cancellationToken).ConfigureAwait(false);
            _ = await repository.ListAsync(EntityQuery.All<ConformanceOrder>(), cancellationToken).ConfigureAwait(false);
            _ = await repository.CountAsync(EntityQuery.All<ConformanceOrder>(), cancellationToken).ConfigureAwait(false);
            _ = await repository.AddRangeAsync([], cancellationToken).ConfigureAwait(false);
            _ = await repository.UpdateRangeAsync([], cancellationToken).ConfigureAwait(false);
            _ = await repository.RemoveRangeAsync([], cancellationToken).ConfigureAwait(false);
            observed.Add(("read", unitOfWork.HasTransaction));
            (await repository.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "fresh" }, cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            observed.Add(("written", unitOfWork.HasTransaction));
            var stored = (await repository.FindAsync(existing, cancellationToken).ConfigureAwait(false)).ValueOrThrow();
            stored.Name = "changed";
            (await repository.UpdateAsync(stored, cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            observed.Add(("written again", unitOfWork.HasTransaction));
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal([("begun", false), ("read", false), ("written", true), ("written again", true)], observed);
        Assert.NotNull(committed);
        Assert.Equal(UnitOfWorkState.Committed, committed.State);
        Assert.False(committed.HasTransaction);

        // 第一次写入的结果是失败（批量更新一个不存在的实体，逐项 NotFound）：事务照样已开启
        var failedWriteOpened = false;
        IUnitOfWork? afterFailure = null;
        var rejection = Error.Literal("CONFORMANCE_VALIDATION_001", "Rejected by the business rule.");
        var failed = await RunAsync(host, async context =>
        {
            afterFailure = context.Services.GetRequiredService<IUnitOfWork>();
            var missing = await Repository<ConformanceOrder, Guid>(context).UpdateRangeAsync([new ConformanceOrder { Id = Guid.CreateVersion7() }], context.CancellationToken).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.NotFound, Assert.Single(missing).Error?.Code);
            failedWriteOpened = afterFailure.HasTransaction;
            return rejection;
        }).ConfigureAwait(false);

        Assert.Equal(rejection, failed.Error);
        Assert.True(failedWriteOpened);
        Assert.NotNull(afterFailure);
        Assert.Equal(UnitOfWorkState.RolledBack, afterFailure.State);
        Assert.False(afterFailure.HasTransaction);

        // 未处理异常：中间件回滚之后为假
        IUnitOfWork? afterException = null;
        var crashed = await RunAsync(host, async context =>
        {
            afterException = context.Services.GetRequiredService<IUnitOfWork>();
            (await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "crashed" }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            throw new InvalidOperationException("The handler crashed.");
        }).ConfigureAwait(false);

        Assert.Equal(KernelErrors.Unexpected, crashed.Error?.Code);
        Assert.NotNull(afterException);
        Assert.Equal(UnitOfWorkState.RolledBack, afterException.State);
        Assert.False(afterException.HasTransaction);

        // 管道外：未开始为假，开始后为假，写入后为真，提交之后为假；在作用域释放之前看（释放时 Provider 回滚未结束的事务，会掩盖没有复位的实现）
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var manual = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            Assert.False(manual.HasTransaction);
            await manual.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None).ConfigureAwait(false);
            Assert.False(manual.HasTransaction);
            (await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>().AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "manual" }, CancellationToken.None).ConfigureAwait(false)).ThrowIfFailure();
            Assert.True(manual.HasTransaction);
            (await manual.CommitAsync(CancellationToken.None).ConfigureAwait(false)).ThrowIfFailure();
            Assert.False(manual.HasTransaction);
        }

        // 管道外：批量写入后为真，回滚之后为假
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var manual = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await manual.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None).ConfigureAwait(false);
            var batch = await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceItem, int>>().AddRangeAsync([new ConformanceItem { Id = 29, Label = "manual" }], CancellationToken.None).ConfigureAwait(false);
            Fold(batch).ThrowIfFailure();
            Assert.True(manual.HasTransaction);
            await manual.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.False(manual.HasTransaction);
        }

        Assert.Equal("changed", (await FindAsync<ConformanceOrder, Guid>(host, existing).ConfigureAwait(false)).ValueOrThrow().Name);
        Assert.Equal(3, await CountAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>()).ConfigureAwait(false));
        Assert.False((await FindAsync<ConformanceItem, int>(host, 29).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 30：只读工作单元里从不开事务
    /// </summary>
    /// <remarks>
    /// 只读操作里读、被拒绝的单条与批量写入、空批量之后 <see cref="IUnitOfWork.HasTransaction"/> 都为假，Provider 的直接写入入口抛出 <see cref="PersistenceErrors.ReadOnly"/> 之后也为假；管道外以 <c>IsReadOnly: true</c> 开启的工作单元同样
    /// </remarks>
    [Fact]
    public async Task HasTransaction_IsAlwaysFalse_InAReadOnlyUnitOfWork()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var existing = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = existing, Name = "existing" }, context.CancellationToken)).ConfigureAwait(false)).ThrowIfFailure();
        var ensure = EnsureTransaction;
        var observed = new List<bool>();
        var codes = new List<string?>();
        IUnitOfWork? unitOfWork = null;

        var result = await InvokeScriptAsync(host, ReadScriptOperation.Id, async context =>
        {
            unitOfWork = context.Services.GetRequiredService<IUnitOfWork>();
            var repository = Repository<ConformanceOrder, Guid>(context);
            var cancellationToken = context.CancellationToken;
            observed.Add(unitOfWork.HasTransaction);
            _ = await repository.FindAsync(existing, cancellationToken).ConfigureAwait(false);
            _ = await repository.CountAsync(EntityQuery.All<ConformanceOrder>(), cancellationToken).ConfigureAwait(false);
            observed.Add(unitOfWork.HasTransaction);
            codes.Add((await repository.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7() }, cancellationToken).ConfigureAwait(false)).Error?.Code);
            observed.Add(unitOfWork.HasTransaction);
            codes.AddRange((await repository.RemoveRangeAsync([new ConformanceOrder { Id = existing }], cancellationToken).ConfigureAwait(false)).Select(static r => r.Error?.Code));
            _ = await repository.UpdateRangeAsync([], cancellationToken).ConfigureAwait(false);
            observed.Add(unitOfWork.HasTransaction);
            if (ensure is not null)
            {
                // 直接写入入口在只读工作单元里抛出，不开事务
                try
                {
                    await ensure(unitOfWork, cancellationToken).ConfigureAwait(false);
                    codes.Add(null);
                }
                catch (FrameworkException exception)
                {
                    codes.Add(exception.Code);
                }

                observed.Add(unitOfWork.HasTransaction);
            }

            return Result.Success();
        }).ConfigureAwait(false);

        Assert.Equal(PersistenceErrors.ReadOnly, result.Error?.Code);
        Assert.All(codes, static code => Assert.Equal(PersistenceErrors.ReadOnly, code));
        Assert.Equal(ensure is null ? 2 : 3, codes.Count);
        Assert.Equal(Enumerable.Repeat(false, ensure is null ? 4 : 5), observed);
        Assert.NotNull(unitOfWork);
        Assert.False(unitOfWork.HasTransaction);

        // 管道外的只读工作单元：读与被拒绝的写入之后为假，提交返回拒绝、之后仍为假
        await using var scope = host.Services.CreateAsyncScope();
        var manual = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await manual.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true }, CancellationToken.None).ConfigureAwait(false);
        var orders = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>();
        Assert.True((await orders.FindAsync(existing, CancellationToken.None).ConfigureAwait(false)).HasValue);
        Assert.Equal(PersistenceErrors.ReadOnly, (await orders.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7() }, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.False(manual.HasTransaction);
        Assert.Equal(PersistenceErrors.ReadOnly, (await manual.CommitAsync(CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.False(manual.HasTransaction);

        // 读主库的只读工作单元仍是只读的；没有从库的 Provider 上这个选项没有作用
        await using var pinnedScope = host.Services.CreateAsyncScope();
        var pinned = pinnedScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await pinned.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true, ReadsPrimary = true }, CancellationToken.None).ConfigureAwait(false);
        var pinnedOrders = pinnedScope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>();
        Assert.True(pinned.Options.IsReadOnly);
        Assert.True((await pinnedOrders.FindAsync(existing, CancellationToken.None).ConfigureAwait(false)).HasValue);
        Assert.Equal(PersistenceErrors.ReadOnly, (await pinnedOrders.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7() }, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.False(pinned.HasTransaction);
        Assert.Equal(PersistenceErrors.ReadOnly, (await pinned.CommitAsync(CancellationToken.None).ConfigureAwait(false)).Error?.Code);

        Assert.Equal(1, await CountAsync<ConformanceOrder, Guid>(host, EntityQuery.All<ConformanceOrder>()).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 31：Provider 的直接写入入口开启事务
    /// </summary>
    /// <remarks>
    /// 只对给出 <see cref="EnsureTransaction"/> 的 Provider：调用入口之后 <see cref="IUnitOfWork.HasTransaction"/> 为真，重复调用与随后的仓储写入仍为真，提交之后为假；管道外开始后调用入口为真，提交、回滚之后为假；没有活动工作单元（未开始、已提交）时入口抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>、仍为假
    /// </remarks>
    [Fact]
    public async Task HasTransaction_OpensOnTheDirectWriteEntry()
    {
        var ensure = EnsureTransaction;
        Assert.SkipWhen(ensure is null, "The provider has no direct write entry; override EnsureTransaction to cover it.");
        await using var host = await StartAsync().ConfigureAwait(false);
        var id = Guid.CreateVersion7();
        var observed = new List<bool>();
        IUnitOfWork? committed = null;

        var result = await RunAsync(host, async context =>
        {
            var unitOfWork = committed = context.Services.GetRequiredService<IUnitOfWork>();
            observed.Add(unitOfWork.HasTransaction);
            await ensure!(unitOfWork, context.CancellationToken).ConfigureAwait(false);
            observed.Add(unitOfWork.HasTransaction);
            // 重复调用复用已开启的事务，之后仓储写入也在同一事务里
            await ensure(unitOfWork, context.CancellationToken).ConfigureAwait(false);
            observed.Add(unitOfWork.HasTransaction);
            (await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = id, Name = "direct" }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            observed.Add(unitOfWork.HasTransaction);
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal([false, true, true, true], observed);
        Assert.NotNull(committed);
        Assert.False(committed.HasTransaction);
        Assert.Equal("direct", (await FindAsync<ConformanceOrder, Guid>(host, id).ConfigureAwait(false)).ValueOrThrow().Name);

        // 管道外：未开始时入口抛出；开始后调用入口为真，提交之后为假、入口再抛出；都在作用域释放之前看
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var manual = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notStarted = await Assert.ThrowsAsync<FrameworkException>(async () => await ensure!(manual, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, notStarted.Code);
            Assert.False(manual.HasTransaction);
            await manual.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None).ConfigureAwait(false);
            await ensure!(manual, CancellationToken.None).ConfigureAwait(false);
            Assert.True(manual.HasTransaction);
            (await manual.CommitAsync(CancellationToken.None).ConfigureAwait(false)).ThrowIfFailure();
            Assert.False(manual.HasTransaction);
            var committedAlready = await Assert.ThrowsAsync<FrameworkException>(async () => await ensure!(manual, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
            Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, committedAlready.Code);
            Assert.False(manual.HasTransaction);
        }

        // 管道外：调用入口之后回滚为假
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var manual = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await manual.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None).ConfigureAwait(false);
            await ensure!(manual, CancellationToken.None).ConfigureAwait(false);
            Assert.True(manual.HasTransaction);
            await manual.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.False(manual.HasTransaction);
        }
    }

    /// <summary>
    /// 用例 32：绑定命名连接的工作单元只报告那个连接上的事务
    /// </summary>
    /// <remarks>
    /// 只对给出 <see cref="NamedConnection"/> 的 Provider：绑定它的工作单元开始与读取之后为假，写入之后为真，提交、回滚之后为假；只读的绑定连接工作单元恒为假。业务调用（不碰命名连接）里另开作用域、绑定连接的工作单元写入并提交，不让业务调用的工作单元变真；业务调用写过之后另开的工作单元开始时仍为假，各报各的
    /// </remarks>
    [Fact]
    public async Task HasTransaction_InAUnitOfWorkBoundToANamedConnection_ReportsOnlyThatConnection()
    {
        var connection = NamedConnection;
        Assert.SkipWhen(connection is null, "The provider does not support named connections; override NamedConnection to cover them.");
        await using var host = await StartAsync().ConfigureAwait(false);
        var committedLease = Guid.CreateVersion7();
        var rolledBackLease = Guid.CreateVersion7();

        // 绑定连接的可写工作单元：开始与读取之后为假，写入之后为真，提交之后为假
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions { Connection = connection }, CancellationToken.None).ConfigureAwait(false);
            var leases = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceLease, Guid>>();
            Assert.False(unitOfWork.HasTransaction);
            Assert.False((await leases.FindAsync(committedLease, CancellationToken.None).ConfigureAwait(false)).HasValue);
            Assert.False(unitOfWork.HasTransaction);
            (await leases.AddAsync(new ConformanceLease { Id = committedLease, Holder = "first" }, CancellationToken.None).ConfigureAwait(false)).ThrowIfFailure();
            Assert.True(unitOfWork.HasTransaction);
            (await unitOfWork.CommitAsync(CancellationToken.None).ConfigureAwait(false)).ThrowIfFailure();
            Assert.False(unitOfWork.HasTransaction);
        }

        // 回滚之后为假，写入不留下
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions { Connection = connection }, CancellationToken.None).ConfigureAwait(false);
            (await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceLease, Guid>>().AddAsync(new ConformanceLease { Id = rolledBackLease, Holder = "second" }, CancellationToken.None).ConfigureAwait(false)).ThrowIfFailure();
            Assert.True(unitOfWork.HasTransaction);
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.False(unitOfWork.HasTransaction);
        }

        // 只读的绑定连接工作单元：读与被拒绝的写入之后都为假
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true, Connection = connection }, CancellationToken.None).ConfigureAwait(false);
            var leases = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceLease, Guid>>();
            Assert.Equal("first", (await leases.FindAsync(committedLease, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Holder);
            Assert.False((await leases.FindAsync(rolledBackLease, CancellationToken.None).ConfigureAwait(false)).HasValue);
            Assert.Equal(PersistenceErrors.ReadOnly, (await leases.AddAsync(new ConformanceLease { Id = Guid.CreateVersion7() }, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.False(unitOfWork.HasTransaction);
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.False(unitOfWork.HasTransaction);
        }

        // 业务调用里另开作用域写命名连接：两个工作单元各报各的
        var business = new List<bool>();
        var bound = new List<bool>();
        var result = await RunAsync(host, async context =>
        {
            var unitOfWork = context.Services.GetRequiredService<IUnitOfWork>();
            await WriteLeaseAsync(host, connection!, bound, context.CancellationToken).ConfigureAwait(false);
            business.Add(unitOfWork.HasTransaction);
            (await Repository<ConformanceOrder, Guid>(context).AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "business" }, context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            business.Add(unitOfWork.HasTransaction);
            await WriteLeaseAsync(host, connection!, bound, context.CancellationToken).ConfigureAwait(false);
            business.Add(unitOfWork.HasTransaction);
            return Result.Success();
        }).ConfigureAwait(false);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal([false, true, true], business);
        Assert.Equal([false, true, false, false, true, false], bound);

        // 在新作用域里开绑定连接的可写工作单元写一条租约并提交，依次记下开始、写入、提交之后的值
        static async Task WriteLeaseAsync(TestHost host, string connection, List<bool> observed, CancellationToken cancellationToken)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions { Connection = connection }, cancellationToken).ConfigureAwait(false);
            observed.Add(unitOfWork.HasTransaction);
            (await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceLease, Guid>>().AddAsync(new ConformanceLease { Id = Guid.CreateVersion7(), Holder = "bound" }, cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            observed.Add(unitOfWork.HasTransaction);
            (await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            observed.Add(unitOfWork.HasTransaction);
        }
    }

    /// <summary>
    /// 经套件操作在一次完整管道调用内执行 <paramref name="script"/>，其返回值作为调用结果
    /// </summary>
    /// <remarks>
    /// <paramref name="operationId"/> 选只读的 <see cref="ReadScriptOperation"/> 或可写的 <see cref="WriteScriptOperation"/>
    /// </remarks>
    private static async Task<Result> InvokeScriptAsync(TestHost host, string operationId, Func<InvocationContext, Task<Result>> script)
    {
        var key = host.Services.GetRequiredService<ConformanceScripts>().Add(script);
        var result = await host.InvokeOperationAsync<ConformanceScriptRequest, ConformanceScriptResponse>(operationId, new ConformanceScriptRequest(key)).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : result.Error!;
    }

    /// <summary>
    /// 取调用作用域内的仓储
    /// </summary>
    protected static IRepository<TEntity, TKey> Repository<TEntity, TKey>(InvocationContext context)
        where TEntity : class, IEntity<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services.GetRequiredService<IRepository<TEntity, TKey>>();
    }

    /// <summary>
    /// 在新调用内按标识查找
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
    /// 在新调用内按标识集合查找
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
    /// 把逐项结果折成一个：第一个失败，否则成功
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
    /// 在新调用内查询
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
    /// 在新调用内计数
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
