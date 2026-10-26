// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 测试宿主的装配与种数据：内存持久化 + 内存本地化 + 数据库多租户；写入经完整管道（Transaction 阶段提供工作单元）。
/// </summary>
internal static class TenantHosts
{
    /// <summary>
    /// 启动装配了内存持久化、内存本地化与数据库多租户的宿主。
    /// </summary>
    public static Task<TestHost> StartAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<IServiceCollection>? services = null)
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseMultiTenancyFeature>(),
            configuration,
            services);

    /// <summary>
    /// 经 <see cref="TenantRecordStore.UpsertAsync"/> 在一次调用里种入租户；任一项失败即抛出。
    /// </summary>
    public static async Task SeedAsync(TestHost host, IReadOnlyList<TenantInfo> tenants)
    {
        var outcome = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<TenantRecordStore>().UpsertAsync(tenants, context.CancellationToken);
            foreach (var result in results)
            {
                result.Outcome.ThrowIfFailure();
            }
        });
        outcome.ThrowIfFailure();
    }

    /// <summary>
    /// 绕过 <see cref="TenantRecordStore"/> 直接经仓储改一行（不失效缓存），用来验证缓存命中。
    /// </summary>
    public static async Task EditRowAsync(TestHost host, string tenantId, Action<TenantRecord> edit)
    {
        var outcome = await host.InvokeAsync(async context =>
        {
            var repository = context.Services.GetRequiredService<IRepository<TenantRecord, string>>();
            var row = (await repository.FindAsync(tenantId, context.CancellationToken)).ValueOrThrow();
            edit(row);
            (await repository.UpdateAsync(row, context.CancellationToken)).ThrowIfFailure();
        });
        outcome.ThrowIfFailure();
    }

    /// <summary>
    /// 绕过 <see cref="TenantRecordStore"/> 直接经仓储加一行（不失效缓存）。
    /// </summary>
    public static async Task AddRowAsync(TestHost host, TenantRecord row)
    {
        var outcome = await host.InvokeAsync(async context =>
            (await context.Services.GetRequiredService<IRepository<TenantRecord, string>>().AddAsync(row, context.CancellationToken)).ThrowIfFailure());
        outcome.ThrowIfFailure();
    }
}
