// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 内存租户表的租户管理跑一致性套件
/// </summary>
public sealed class InMemoryTenantAdministrationConformanceTests : TenantAdministrationConformance, IAsyncDisposable
{
    /// <summary>
    /// 本用例启动的宿主
    /// </summary>
    private TestHost? _host;

    /// <inheritdoc />
    protected override async ValueTask<ITenantStore> StartAsync()
    {
        _host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryMultiTenancyFeature>());
        return _host.Services.GetRequiredService<ITenantStore>();
    }

    /// <inheritdoc />
    protected override ValueTask<T> AdministerAsync<T>(Func<ITenantAdministration, CancellationToken, ValueTask<T>> action)
    {
        var host = _host ?? throw new InvalidOperationException("StartAsync was not called.");
        return action(host.Services.GetRequiredService<ITenantAdministration>(), CancellationToken.None);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }
    }
}
