// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Conformance;
using XiHan.Framework.Security.Database.Grants;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 数据库授予存储跑一致性套件。
/// </summary>
/// <remarks>
/// 每个用例启动一个装配了内存持久化的宿主，经 <see cref="PermissionGrantRecordStore"/> 种入套件的授予；用例结束后停止宿主。
/// </remarks>
public sealed class DatabasePermissionGrantStoreConformanceTests : PermissionGrantStoreConformance, IAsyncDisposable
{
    /// <summary>
    /// 本用例启动的宿主。
    /// </summary>
    /// <remarks>
    /// 释放时停止。
    /// </remarks>
    private readonly List<TestHost> _hosts = [];

    /// <inheritdoc />
    protected override async ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants)
    {
        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseSecurityFeature>());
        _hosts.Add(host);
        var seeded = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync(grants, context.CancellationToken);
            Assert.All(results, static r => Assert.True(r.Outcome.IsSuccess));
        });
        seeded.ThrowIfFailure();
        return host.Services.GetRequiredService<IPermissionGrantStore>();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        _hosts.Clear();
    }
}
