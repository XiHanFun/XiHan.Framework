// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 撞上唯一索引时返回唯一键重复（ADR-0072）。
/// </summary>
/// <remarks>
/// 仓储返回 <c>PERSISTENCE_CONFLICT_003</c>，不让 <see cref="DbUpdateException"/> 逃出（RFC-0005「索引声明」）。
/// 新增（同一批两项同键、另一行同键）、更新把键改成已有的值、软删除撞上同一键已删除的行；之后工作单元只能回滚，调用以同一错误结束。
/// </remarks>
public sealed class EntityFrameworkCoreUniqueKeyConflictTests : IDisposable
{
    /// <summary>
    /// 固定的时刻。
    /// </summary>
    private static readonly DateTimeOffset _at = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task AddRange_WithTwoItemsSharingAKey_ReportsUniqueKeyConflict()
    {
        await using var host = await StartAsync();

        var outcome = await host.InvokeAsync(async context =>
        {
            var results = await Rows(context).AddRangeAsync([Row("acme", "A"), Row("acme", "A")], context.CancellationToken);
            Assert.All(results, static r => Assert.Equal(PersistenceErrors.UniqueKeyConflict, r.Error?.Code));
            Assert.Equal("UX_indexed_row_TeId_Co", results[0].Error!.Details!["index"]);
        });

        // 处理器没有返回失败，工作单元也已标为失败：提交回滚并以同一错误结束
        Assert.Equal(PersistenceErrors.UniqueKeyConflict, outcome.Error?.Code);
        Assert.Equal(0, await CountAsync(host));
    }

    [Fact]
    public async Task Add_WhenAnotherRowHoldsTheKey_ReportsUniqueKeyConflict()
    {
        await using var host = await StartAsync();
        (await host.InvokeAsync(async context => context.SetOutcome(await Rows(context).AddAsync(Row("acme", "A"), context.CancellationToken)))).ThrowIfFailure();

        var outcome = await host.InvokeAsync(async context =>
        {
            var result = await Rows(context).AddAsync(Row("acme", "A"), context.CancellationToken);
            Assert.Equal(PersistenceErrors.UniqueKeyConflict, result.Error?.Code);
            context.SetOutcome(result);
        });

        Assert.Equal(PersistenceErrors.UniqueKeyConflict, outcome.Error?.Code);
        Assert.Equal(1, await CountAsync(host));
    }

    [Fact]
    public async Task Update_ToAKeyAnotherRowHolds_ReportsUniqueKeyConflict()
    {
        await using var host = await StartAsync();
        var second = Row("acme", "B");
        (await host.InvokeAsync(async context => context.SetOutcome(Combine(await Rows(context).AddRangeAsync([Row("acme", "A"), second], context.CancellationToken))))).ThrowIfFailure();

        var outcome = await host.InvokeAsync(async context =>
        {
            var found = (await Rows(context).FindAsync(second.Id, context.CancellationToken)).ValueOrThrow();
            found.Code = "A";
            Assert.Equal(PersistenceErrors.UniqueKeyConflict, (await Rows(context).UpdateAsync(found, context.CancellationToken)).Error?.Code);
        });

        Assert.Equal(PersistenceErrors.UniqueKeyConflict, outcome.Error?.Code);
        await using var scope = host.Services.CreateAsyncScope();
        Assert.Equal(["A", "B"], await scope.ServiceProvider.GetRequiredService<IndexedDbContext>().Set<IndexedRow>().OrderBy(static r => r.Code).Select(static r => r.Code).ToListAsync());
    }

    [Fact]
    public async Task SoftDelete_OfASecondRowWithADeletedTwin_ReportsUniqueKeyConflict()
    {
        await using var host = await StartAsync();
        var first = new SoftDeletedIndexedRow { Id = Guid.CreateVersion7(), Code = "A" };
        var second = new SoftDeletedIndexedRow { Id = Guid.CreateVersion7(), Code = "A" };
        // 删掉第一行之后同一编码可以再建一行
        (await host.InvokeAsync(async context => context.SetOutcome(await Soft(context).AddAsync(first, context.CancellationToken)))).ThrowIfFailure();
        (await host.InvokeAsync(async context => context.SetOutcome(await Soft(context).RemoveAsync(first, context.CancellationToken)))).ThrowIfFailure();
        (await host.InvokeAsync(async context => context.SetOutcome(await Soft(context).AddAsync(second, context.CancellationToken)))).ThrowIfFailure();

        // 再删第二行：唯一索引带删除标记，同一编码已有一行已删除
        var outcome = await host.InvokeAsync(async context => context.SetOutcome(await Soft(context).RemoveAsync(second, context.CancellationToken)));

        Assert.Equal(PersistenceErrors.UniqueKeyConflict, outcome.Error?.Code);
        Assert.Equal("UX_SoftDeletedIndexedRow_Co", outcome.Error!.Details!["index"]);
    }

    /// <summary>
    /// 启动以 <see cref="IndexedDbContext"/> 装配 EF Core 持久化的宿主（启动即建表）。
    /// </summary>
    private Task<TestHost> StartAsync() => TestHost.StartAsync(
        static builder => builder.UseFeature(new EntityFrameworkCorePersistenceFeature<IndexedDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))),
        _database.Configuration,
        static services =>
        {
            services.AddRepository<IndexedRow, Guid>();
            services.AddRepository<SoftDeletedIndexedRow, Guid>();
        });

    /// <summary>
    /// 表里的行数，绕过仓储直接查。
    /// </summary>
    private static async Task<int> CountAsync(TestHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IndexedDbContext>().Set<IndexedRow>().CountAsync();
    }

    /// <summary>
    /// 有唯一索引的实体的仓储。
    /// </summary>
    private static IRepository<IndexedRow, Guid> Rows(InvocationContext context) => context.Services.GetRequiredService<IRepository<IndexedRow, Guid>>();

    /// <summary>
    /// 软删除实体的仓储。
    /// </summary>
    private static IRepository<SoftDeletedIndexedRow, Guid> Soft(InvocationContext context) => context.Services.GetRequiredService<IRepository<SoftDeletedIndexedRow, Guid>>();

    /// <summary>
    /// 逐项结果合成一个：有失败取第一个失败。
    /// </summary>
    private static Result Combine(IReadOnlyList<Result> results) => results.Any(static r => r.IsFailure) ? results.First(static r => r.IsFailure) : Result.Success();

    /// <summary>
    /// 一行记录。
    /// </summary>
    private static IndexedRow Row(string tenantId, string code)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, Code = code, Category = "c", At = _at };
}
