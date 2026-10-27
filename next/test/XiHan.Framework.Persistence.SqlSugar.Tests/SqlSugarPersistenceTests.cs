// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 独有的行为：选项解析、协议约定、游离实体更新与并发戳、只读工作单元、状态机、建表开关与应用自注册的客户端。一致性套件覆盖的通用行为不在此重复。
/// </summary>
public sealed class SqlSugarPersistenceTests : IDisposable
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
        Assert.False(SqlSugarPersistenceOptions.FromConfiguration(Configuration()).InitTables);
        Assert.True(SqlSugarPersistenceOptions.FromConfiguration(Configuration(("XiHan:Persistence:SqlSugar:InitTables", "true"))).InitTables);
        Assert.Throws<ArgumentException>(static () => SqlSugarPersistenceOptions.FromConfiguration(Configuration(("XiHan:Persistence:SqlSugar:InitTables", "yes"))));
    }

    [Fact]
    public async Task Conventions_MapPrimaryKeyAndStamp_AndIgnoreReadOnlyMembers()
    {
        await using var host = await _database.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        var order = client.EntityMaintenance.GetEntityInfo<ConformanceOrder>();
        var id = order.Columns.Single(static c => c.PropertyName == nameof(ConformanceOrder.Id));
        Assert.True(id.IsPrimarykey);
        Assert.False(id.IsIdentity);
        Assert.Equal(32, order.Columns.Single(static c => c.PropertyName == nameof(ConformanceOrder.ConcurrencyStamp)).Length);
        // 只读的领域事件集合不能被当作列映射
        Assert.DoesNotContain(order.Columns, static c => c.PropertyName == nameof(ConformanceOrder.DomainEvents) && !c.IsIgnore);

        // 整数标识由用例自行指定，不能交给数据库自增
        var item = client.EntityMaintenance.GetEntityInfo<ConformanceItem>().Columns.Single(static c => c.PropertyName == nameof(ConformanceItem.Id));
        Assert.True(item.IsPrimarykey);
        Assert.False(item.IsIdentity);
    }

    [Fact]
    public async Task Conventions_MapStringColumnLengths_FromDataAnnotations()
    {
        await using var host = await _database.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var columns = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().EntityMaintenance.GetEntityInfo<LengthAnnotatedRecord>().Columns;
        EntityColumnInfo Column(string name) => columns.Single(c => c.PropertyName == name);

        // [StringLength(n)] / [MaxLength(n)] 给长度，[LongText] 与无参 [MaxLength] 是数据库的长文本类型，没有注解的沿用 SqlSugar 默认（没有长度、没有类型）
        Assert.Equal(64, Column(nameof(LengthAnnotatedRecord.Code)).Length);
        Assert.Equal(StaticConfig.CodeFirst_BigString, Column(nameof(LengthAnnotatedRecord.Body)).DataType);
        Assert.Equal(StaticConfig.CodeFirst_BigString, Column(nameof(LengthAnnotatedRecord.Document)).DataType);
        Assert.Equal(10, Column(nameof(LengthAnnotatedRecord.Tag)).Length);
        Assert.True(Column(nameof(LengthAnnotatedRecord.Tag)).IsNullable);
        Assert.Equal(0, Column(nameof(LengthAnnotatedRecord.Plain)).Length);
        Assert.True(string.IsNullOrEmpty(Column(nameof(LengthAnnotatedRecord.Plain)).DataType));
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
    public async Task Update_WithFreshDetachedEntity_WritesValuesAndRefreshesStamp()
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
    public async Task Remove_WithDetachedEntity_SoftDeletesCurrentRow_AndCopiesMarkBack()
    {
        await using var host = await _database.StartHostAsync();
        var id = Guid.CreateVersion7();
        (await RunAsync(host, ctx => Repository(ctx).AddAsync(new ConformanceOrder { Id = id, Name = "keep-me" }, ctx.CancellationToken))).ThrowIfFailure();

        // 游离实例只带标识：删除按当前行打标，不把游离实例里的空值写回其他列
        var detached = new ConformanceOrder { Id = id };
        var removed = await RunAsync(host, ctx => Repository(ctx).RemoveAsync(detached, ctx.CancellationToken));

        Assert.True(removed.IsSuccess, removed.ToString());
        Assert.True(detached.IsDeleted);
        Assert.NotNull(detached.DeletedAt);
        ConformanceOrder? tombstone = null;
        (await RunAsync(host, async ctx =>
        {
            tombstone = (await Repository(ctx).ListAsync(EntityQuery.All<ConformanceOrder>() with { IncludeDeleted = true }, ctx.CancellationToken)).Single();
            return Result.Success();
        })).ThrowIfFailure();
        Assert.Equal("keep-me", tombstone!.Name);
        Assert.True(tombstone.IsDeleted);
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
    public async Task Feature_WithoutInitTables_LeavesSchemaToApplication()
    {
        // 只给连接绑定、不开 InitTables：启动后数据库应保持空白
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
            [new("XiHan:Bindings:db", _database.ConnectionString)],
            services => services.AddRepository<ConformanceOrder, Guid>());
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        Assert.False(client.DbMaintenance.IsAnyTable(client.EntityMaintenance.GetTableName<ConformanceOrder>(), false));
    }

    [Fact]
    public async Task Feature_UsesClientRegisteredByApplication()
    {
        // 应用自己注册客户端并应用约定；特性不再创建客户端，建表与仓储都用应用的那个
        await using var host = await TestHost.StartAsync(
            static builder => builder.UseFeature(new SqlSugarPersistenceFeature()),
            [new("XiHan:Persistence:SqlSugar:InitTables", "true")],
            services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddScoped<ISqlSugarClient>(_ => new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig
                {
                    DbType = DbType.Sqlite,
                    ConnectionString = _database.ConnectionString,
                    IsAutoCloseConnection = true,
                })));
                services.AddRepository<ConformanceOrder, Guid>();
            });
        var id = Guid.CreateVersion7();

        (await RunAsync(host, ctx => Repository(ctx).AddAsync(new ConformanceOrder { Id = id, Name = "own-client" }, ctx.CancellationToken))).ThrowIfFailure();

        ConformanceOrder? current = null;
        (await RunAsync(host, async ctx =>
        {
            current = (await Repository(ctx).FindAsync(id, ctx.CancellationToken)).ValueOrThrow();
            return Result.Success();
        })).ThrowIfFailure();
        Assert.Equal("own-client", current!.Name);
        Assert.False(string.IsNullOrEmpty(current.ConcurrencyStamp));
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
