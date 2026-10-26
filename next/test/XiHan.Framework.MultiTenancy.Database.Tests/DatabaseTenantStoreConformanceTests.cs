// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Conformance;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 让 <see cref="DatabaseTenantStore"/>（对着内存持久化）跑完整套 <see cref="TenantStoreConformance"/> 用例；套件每次要一个独立的存储，所以每次启动一个宿主，用例结束时一并释放。
/// </summary>
public sealed class DatabaseTenantStoreConformanceTests : TenantStoreConformance, IAsyncDisposable
{
    /// <summary>
    /// 本用例启动的宿主；释放时一并停止。
    /// </summary>
    private readonly List<TestHost> _hosts = [];

    /// <summary>
    /// 启动宿主、经 <see cref="TenantRecordStore"/> 种入租户，返回宿主里的目录。
    /// </summary>
    protected override async ValueTask<ITenantStore> CreateStoreAsync(IReadOnlyList<TenantInfo> tenants)
    {
        var host = await TenantHosts.StartAsync();
        _hosts.Add(host);
        await TenantHosts.SeedAsync(host, tenants);
        return host.Services.GetRequiredService<ITenantStore>();
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
