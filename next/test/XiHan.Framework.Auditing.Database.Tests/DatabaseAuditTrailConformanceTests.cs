// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Auditing.Conformance;
using XiHan.Framework.Auditing.Database.Tests.Fixtures;
using XiHan.Framework.Auditing.Database.Trail;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Database.Tests;

/// <summary>
/// 让 <see cref="DatabaseAuditTrail"/>（对着内存持久化）跑完整套 <see cref="AuditTrailConformance"/> 用例。
/// </summary>
/// <remarks>
/// 套件不释放宿主，所以每个用例在生命周期钩子里启动与释放自己的宿主。
/// </remarks>
public sealed class DatabaseAuditTrailConformanceTests : AuditTrailConformance, IAsyncLifetime
{
    /// <summary>
    /// 数据库夹具：接收器是写入后立刻刷写的装饰器，查询直接是数据库审计。
    /// </summary>
    private sealed class Fixture : AuditTrailFixture
    {
        /// <summary>
        /// 创建。
        /// </summary>
        public Fixture(DatabaseAuditTrail trail)
        {
            Trail = trail;
            Sink = new FlushingAuditSink(trail);
        }

        /// <inheritdoc />
        public override IAuditSink Sink { get; }

        /// <inheritdoc />
        public override IAuditTrail Trail { get; }
    }

    /// <summary>
    /// 本用例的宿主。
    /// </summary>
    private TestHost? _host;

    /// <summary>
    /// 启动装配了内存持久化、内存本地化与数据库审计的宿主。
    /// </summary>
    public async ValueTask InitializeAsync()
        => _host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseAuditingFeature>());

    /// <summary>
    /// 停止宿主。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }
    }

    /// <inheritdoc />
    protected override Task<AuditTrailFixture> CreateFixtureAsync()
    {
        var host = _host ?? throw new InvalidOperationException("The host has not been started.");
        return Task.FromResult<AuditTrailFixture>(new Fixture(host.Services.GetRequiredService<DatabaseAuditTrail>()));
    }
}
