// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// EF Core Provider 独有的行为
/// </summary>
/// <remarks>
/// 选项解析、模型约定、游离实体更新与并发戳、新增撞上主键约束（检查之后同一标识被占）时的标识重复与只能回滚的提交、残余变更不改创建时间、只读工作单元、状态机与建表开关。一致性套件覆盖的通用行为不在此重复
/// </remarks>
public sealed class EntityFrameworkCorePersistenceTests : IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 测试结束后删除临时数据库文件
    /// </summary>
    public void Dispose() => _database.Dispose();

    /// <summary>
    /// 用键值对构造内存配置
    /// </summary>
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
        // 只读的领域事件集合不能被当作列映射
        Assert.Null(model.FindEntityType(typeof(ConformanceOrder))!.FindProperty(nameof(ConformanceOrder.DomainEvents)));
    }

    [Fact]
    public async Task Update_WithStaleDetachedEntity_ReturnsConcurrencyConflict()
    {
        await using var host = await _database.StartHostAsync();
        var id = Guid.CreateVersion7();
        (await RunAsync(host, ctx => Repository(ctx).AddAsync(new ConformanceOrder { Id = id, Name = "v1" }, ctx.CancellationToken))).ThrowIfFailure();

        // 复制一份带当时并发戳的游离实体，之后另一个调用更新同一行使这份戳过期
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

        // 游离实例本身要拿到新的修改时间与并发戳，才能用于下一次更新
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
    public async Task Add_WhenTheIdIsTakenBetweenCheckAndWrite_ReturnsDuplicate_AndTheCommitOnlyRollsBack()
    {
        var raced = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await using var host = await _database.StartHostAsync(interceptor: new RacingInsertInterceptor(raced));
        Result added = Result.Success();

        // 处理器忽略了新增的失败，接着写别的行并返回成功：工作单元已标为失败，提交只回滚并返回同一错误
        var outcome = await RunAsync(host, async ctx =>
        {
            added = await Repository(ctx).AddAsync(new ConformanceOrder { Id = raced, Name = "mine" }, ctx.CancellationToken);
            (await Repository(ctx).AddAsync(new ConformanceOrder { Id = other, Name = "other" }, ctx.CancellationToken)).ThrowIfFailure();
            return Result.Success();
        });

        Assert.Equal(PersistenceErrors.Duplicate, added.Error?.Code);
        Assert.Equal(raced, added.Error!.Details!["id"]);
        Assert.Equal(PersistenceErrors.Duplicate, outcome.Error?.Code);
        Assert.False((await FindAsync(host, raced)).HasValue);
        Assert.False((await FindAsync(host, other)).HasValue);
    }

    [Fact]
    public async Task AddRange_WhenAnIdIsTakenBetweenCheckAndWrite_ReportsDuplicateForTheWholeWrite()
    {
        var raced = Guid.CreateVersion7();
        await using var host = await _database.StartHostAsync(interceptor: new RacingInsertInterceptor(raced));
        IReadOnlyList<Result> results = [];

        var outcome = await RunAsync(host, async ctx =>
        {
            results = await Repository(ctx).AddRangeAsync([new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "a" }, new ConformanceOrder { Id = raced, Name = "b" }], ctx.CancellationToken);
            return Result.Success();
        });

        // 一次多行写入整体失败：两项都报告标识重复，指向撞上的标识
        Assert.All(results, result =>
        {
            Assert.Equal(PersistenceErrors.Duplicate, result.Error?.Code);
            Assert.Equal(raced, result.Error!.Details!["id"]);
        });
        Assert.Equal(PersistenceErrors.Duplicate, outcome.Error?.Code);
        Assert.Empty(await ListAllAsync(host));
    }

    [Fact]
    public async Task Commit_KeepsStoredCreationTimeAndTenant_ForTrackedEntitiesChangedOutsideTheRepository()
    {
        await using var host = await _database.StartHostAsync();
        var id = Guid.CreateVersion7();
        (await RunAsync(host, ctx => Repository(ctx).AddAsync(new ConformanceOrder { Id = id, Name = "v1" }, ctx.CancellationToken))).ThrowIfFailure();
        var createdAt = (await FindAsync(host, id)).ValueOrThrow().CreatedAt;

        // 绕过仓储直接改跟踪实例：提交时保存残余变更，但创建时间回到原值
        (await RunAsync(host, async ctx =>
        {
            var order = (await Repository(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow();
            order.Name = "v2";
            order.CreatedAt = createdAt.AddYears(-1);
            return Result.Success();
        })).ThrowIfFailure();

        var current = (await FindAsync(host, id)).ValueOrThrow();
        Assert.Equal("v2", current.Name);
        Assert.Equal(createdAt, current.CreatedAt);
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

        // 仓储在写入前就拒绝、不触数据库；拒绝把工作单元记为失败，忽略了这次失败的提交仍返回同一错误
        Assert.Equal(PersistenceErrors.ReadOnly, added.Error?.Code);
        var commit = await unitOfWork.CommitAsync(CancellationToken.None);
        Assert.Equal(PersistenceErrors.ReadOnly, commit.Error?.Code);
        Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
    }

    [Fact]
    public async Task ReadOnlyUnitOfWork_DirectWriteEntry_Throws_AndUntrackedChangesAreRejectedAtCommit()
    {
        await using var host = await _database.StartHostAsync();
        var id = Guid.CreateVersion7();
        (await RunAsync(host, context => Repository(context).AddAsync(new ConformanceOrder { Id = id, Name = "v1" }, context.CancellationToken))).ThrowIfFailure();

        // 直接用 DbContext 写入之前的事务入口：只读工作单元抛出携带 PERSISTENCE_UNEXPECTED_001 的异常，写入不会执行
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = (EntityFrameworkCoreUnitOfWork)scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true), CancellationToken.None);
            var thrown = await Assert.ThrowsAsync<FrameworkException>(async () => await unitOfWork.EnsureTransactionAsync(CancellationToken.None));
            Assert.Equal(PersistenceErrors.ReadOnly, thrown.Code);
            Assert.Equal(PersistenceErrors.ReadOnly, (await unitOfWork.CommitAsync(CancellationToken.None)).Error?.Code);
        }

        // 绕过仓储改了跟踪实体而未保存：提交时拒绝并回滚，存储不变
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true), CancellationToken.None);
            var loaded = (await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>().FindAsync(id, CancellationToken.None)).ValueOrThrow();
            loaded.Name = "changed";
            var commit = await unitOfWork.CommitAsync(CancellationToken.None);
            Assert.Equal(PersistenceErrors.ReadOnly, commit.Error?.Code);
            Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
        }

        Assert.Equal("v1", (await FindAsync(host, id)).ValueOrThrow().Name);
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
        // 只给连接绑定、不开 EnsureCreated：启动后数据库应保持空白
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
            [new("XiHan:Bindings:db", _database.ConnectionString)],
            services => services.AddRepository<ConformanceOrder, Guid>());
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>();

        Assert.False(await context.Database.GetService<IRelationalDatabaseCreator>().HasTablesAsync());
    }

    /// <summary>
    /// 取调用作用域内的订单仓储
    /// </summary>
    private static IRepository<ConformanceOrder, Guid> Repository(InvocationContext context)
        => context.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>();

    /// <summary>
    /// 在一次完整管道调用内执行 <paramref name="action"/>，其返回值作为调用结果
    /// </summary>
    private static async Task<Result> RunAsync(TestHost host, Func<InvocationContext, Task<Result>> action)
        => await host.InvokeAsync(async context => context.SetOutcome(await action(context)));

    /// <summary>
    /// 在新调用内按标识查找订单
    /// </summary>
    private static async Task<Maybe<ConformanceOrder>> FindAsync(TestHost host, Guid id)
    {
        Maybe<ConformanceOrder> found = default;
        (await RunAsync(host, async ctx =>
        {
            found = await Repository(ctx).FindAsync(id, ctx.CancellationToken);
            return Result.Success();
        })).ThrowIfFailure();
        return found;
    }

    /// <summary>
    /// 在新调用内列出全部订单（含已软删除）
    /// </summary>
    private static async Task<IReadOnlyList<ConformanceOrder>> ListAllAsync(TestHost host)
    {
        IReadOnlyList<ConformanceOrder> items = [];
        (await RunAsync(host, async ctx =>
        {
            items = await Repository(ctx).ListAsync(EntityQuery.All<ConformanceOrder>() with { IncludeDeleted = true }, ctx.CancellationToken);
            return Result.Success();
        })).ThrowIfFailure();
        return items;
    }
}
