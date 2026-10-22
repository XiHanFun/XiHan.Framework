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

/// <summary>
/// EF Core Provider 独有的行为：选项解析、模型约定、游离实体更新与并发戳、只读工作单元、状态机与建表开关。一致性套件覆盖的通用行为不在此重复。
/// </summary>
public sealed class EntityFrameworkCorePersistenceTests : IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();

    /// <summary>
    /// 用键值对构造内存配置。
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
    public async Task ReadOnlyUnitOfWork_RejectsWritesImmediately()
    {
        await using var host = await _database.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true), CancellationToken.None);
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>();

        var added = await repository.AddAsync(new ConformanceOrder { Id = Guid.CreateVersion7() }, CancellationToken.None);

        // 与内存实现不同：EF 仓储在写入前就拒绝，所以提交时没有残余变更，工作单元正常提交
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
    /// 取调用作用域内的订单仓储。
    /// </summary>
    private static IRepository<ConformanceOrder, Guid> Repository(InvocationContext context)
        => context.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>();

    /// <summary>
    /// 在一次完整管道调用内执行 <paramref name="action"/>，其返回值作为调用结果。
    /// </summary>
    private static async Task<Result> RunAsync(TestHost host, Func<InvocationContext, Task<Result>> action)
        => await host.InvokeAsync(async context => context.SetOutcome(await action(context)));
}
