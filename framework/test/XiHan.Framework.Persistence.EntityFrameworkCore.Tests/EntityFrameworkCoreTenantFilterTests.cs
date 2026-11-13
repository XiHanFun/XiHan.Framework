// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 直接用 DbContext 查询同样按工作单元的租户过滤（ADR-0224）
/// </summary>
/// <remarks>
/// 仓储自己按租户过滤，但工作单元的 <c>Context</c> 与作用域里的 DbContext 是公开的：没有全局查询过滤器时，直接查询能读到别的租户的行
/// </remarks>
public sealed class EntityFrameworkCoreTenantFilterTests : IDisposable
{
    /// <summary>
    /// 本测试独占的临时数据库
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 删除临时数据库文件
    /// </summary>
    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task DirectContextQueries_SeeOnlyTheUnitOfWorksTenant()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(SqliteDatabase.CreateFeature()),
            [
                .. _database.Configuration,
                new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme"),
                new("XiHan:MultiTenancy:Tenants:beta:DisplayName", "Beta"),
            ],
            static services => services.AddRepository<ConformanceNote, Guid>());
        await AddAsync(host, "acme");
        await AddAsync(host, "beta");

        // 同一个查询在不同租户的调用里各自只看到自己的行：过滤条件按每次查询的上下文取值，不是建模时的那一个
        Assert.Equal(["acme"], await DirectAsync(host, "acme"));
        Assert.Equal(["beta"], await DirectAsync(host, "beta"));
        // 不带租户的调用看不到任何多租户的行，与仓储同一规则
        Assert.Empty(await DirectAsync(host, null));

        // 显式绕开：按名字忽略这一个过滤器
        Assert.Equal(["acme", "beta"], await DirectAsync(host, "acme", ignoreTenantFilter: true));

        // 不在 XiHan 工作单元里的上下文（应用自己建的）不过滤
        await using var standalone = new ConformanceDbContext(new DbContextOptionsBuilder<ConformanceDbContext>().UseSqlite(_database.ConnectionString).Options);
        Assert.Equal(2, await standalone.Set<ConformanceNote>().CountAsync());
    }

    /// <summary>
    /// 在以 <paramref name="tenant"/> 开的调用里经仓储写一条笔记
    /// </summary>
    private static async Task AddAsync(TestHost host, string tenant)
    {
        var note = new ConformanceNote { Id = Guid.CreateVersion7(), TenantId = tenant, Text = tenant };
        (await host.InvokeAsync(async context => context.SetOutcome(await context.Services.GetRequiredService<IRepository<ConformanceNote, Guid>>().AddAsync(note, context.CancellationToken)), baggage: Baggage(tenant))).ThrowIfFailure();
    }

    /// <summary>
    /// 在以 <paramref name="tenant"/> 开的调用里直接用作用域的 DbContext 查询全部笔记的文本
    /// </summary>
    private static async Task<List<string>> DirectAsync(TestHost host, string? tenant, bool ignoreTenantFilter = false)
    {
        List<string> texts = [];
        (await host.InvokeAsync(async context =>
        {
            var notes = context.Services.GetRequiredService<ConformanceDbContext>().Set<ConformanceNote>().AsQueryable();
            if (ignoreTenantFilter)
            {
                notes = notes.IgnoreQueryFilters([ModelBuilderExtensions.TenantQueryFilter]);
            }

            texts = [.. (await notes.Select(static n => n.Text).ToListAsync(context.CancellationToken)).Order(StringComparer.Ordinal)];
        }, baggage: Baggage(tenant))).ThrowIfFailure();
        return texts;
    }

    /// <summary>
    /// 自报租户的 Baggage
    /// </summary>
    private static Dictionary<string, string>? Baggage(string? tenant) => tenant is null ? null : new Dictionary<string, string> { [BaggageKeys.Tenant] = tenant };
}
