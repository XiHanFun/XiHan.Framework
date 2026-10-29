// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Conformance;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 数据库租户存储上分片解析器的一致性用例。
/// </summary>
/// <remarks>
/// 让架在 <see cref="DatabaseTenantStore"/> 上的 <see cref="TenantStoreShardResolver"/> 跑完整套 <see cref="TenantShardResolverConformance"/> 用例。
/// 每次一个宿主，用例结束时一并释放。
/// </remarks>
public sealed class DatabaseTenantShardResolverConformanceTests : TenantShardResolverConformance, IAsyncDisposable
{
    /// <summary>
    /// 本用例启动的宿主。
    /// </summary>
    /// <remarks>
    /// 释放时一并停止。
    /// </remarks>
    private readonly List<TestHost> _hosts = [];

    /// <summary>
    /// 启动宿主、种入租户，返回宿主里被选中的分片解析器。
    /// </summary>
    protected override async ValueTask<ITenantShardResolver> CreateResolverAsync(IReadOnlyList<TenantInfo> tenants)
    {
        var host = await TenantHosts.StartAsync();
        _hosts.Add(host);
        await TenantHosts.SeedAsync(host, tenants);
        return host.Services.GetRequiredService<ITenantShardResolver>();
    }

    /// <summary>
    /// 停止全部宿主。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        _hosts.Clear();
    }
}
