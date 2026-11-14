// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Conformance;
using XiHan.Framework.Security.Database.Tests.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 数据库授予存储在 SQLite 上跑授予管理一致性套件，含另一个实例的用例
/// </summary>
/// <remarks>
/// 两个宿主连同一个 SQLite 文件、登记同一份 <see cref="ICache"/>，模拟共用数据库与缓存后端的两个实例（ADR-0229）
/// </remarks>
public sealed class SqlitePermissionGrantAdministrationConformanceTests : PermissionGrantAdministrationConformance, IAsyncDisposable
{
    /// <summary>
    /// 临时数据库
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 两个实例共用的缓存
    /// </summary>
    private readonly ICache _shared = NewSharedCache();

    /// <summary>
    /// 写入用的实例
    /// </summary>
    private TestHost? _host;

    /// <inheritdoc />
    protected override async ValueTask<IPermissionGrantStore> StartAsync()
    {
        _host = await _database.StartHostAsync(services: s => s.AddSingleton(_shared));
        return _host.Services.GetRequiredService<IPermissionGrantStore>();
    }

    /// <inheritdoc />
    protected override async ValueTask<IPermissionGrantStore?> StartPeerAsync()
        => (await _database.StartHostAsync(services: s => s.AddSingleton(_shared))).Services.GetRequiredService<IPermissionGrantStore>();

    /// <inheritdoc />
    protected override async ValueTask<T> AdministerAsync<T>(Func<IPermissionGrantAdministration, CancellationToken, ValueTask<T>> action)
    {
        var host = _host ?? throw new InvalidOperationException("StartAsync was not called.");
        // 结果经数组带出调用：处理器是委托，调用本身只报告管道的结果
        var result = new T[1];
        (await host.InvokeAsync(async context => result[0] = await action(context.Services.GetRequiredService<IPermissionGrantAdministration>(), context.CancellationToken))).ThrowIfFailure();
        return result[0];
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();

    /// <summary>
    /// 一份独立的内存缓存
    /// </summary>
    private static InMemoryObjectCache NewSharedCache()
    {
        var options = new InMemoryCachingOptions();
        return new InMemoryObjectCache(new InMemoryCacheStore(options, TimeProvider.System), options, TimeProvider.System);
    }
}
