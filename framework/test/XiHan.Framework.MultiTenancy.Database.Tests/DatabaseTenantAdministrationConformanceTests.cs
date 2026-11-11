// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Conformance;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 数据库租户目录的租户管理跑一致性套件
/// </summary>
/// <remarks>
/// 每个用例启动一个装配了内存持久化的宿主，租户管理在一次调用的工作单元里执行；用例结束后停止宿主
/// </remarks>
public sealed class DatabaseTenantAdministrationConformanceTests : TenantAdministrationConformance, IAsyncDisposable
{
    /// <summary>
    /// 本用例启动的宿主
    /// </summary>
    private TestHost? _host;

    /// <inheritdoc />
    protected override async ValueTask<ITenantStore> StartAsync()
    {
        _host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseMultiTenancyFeature>());
        return _host.Services.GetRequiredService<ITenantStore>();
    }

    /// <inheritdoc />
    protected override async ValueTask<T> AdministerAsync<T>(Func<ITenantAdministration, CancellationToken, ValueTask<T>> action)
    {
        var host = _host ?? throw new InvalidOperationException("StartAsync was not called.");
        // 结果经数组带出调用：处理器是委托，调用本身只报告管道的结果
        var result = new T[1];
        (await host.InvokeAsync(async context => result[0] = await action(context.Services.GetRequiredService<ITenantAdministration>(), context.CancellationToken))).ThrowIfFailure();
        return result[0];
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
