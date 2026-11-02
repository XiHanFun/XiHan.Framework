// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Scheduling.Database.Leadership;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 让 <see cref="DatabaseLeaderElection"/>跑完整套 <see cref="LeaderElectionConformance"/> 用例。
/// </summary>
/// <remarks>
/// 对着 SQLite。套件不释放宿主，所以每个用例在生命周期钩子里启动与释放自己的宿主；选举用套件给的时钟构造，租约行的到期时刻也按它写。应用生命周期用例另开一个库，起两个共用它与套件时钟的节点：被测节点装配探针，另一个节点的选举作竞争者。
/// </remarks>
public sealed class DatabaseLeaderElectionConformanceTests : LeaderElectionConformance, IAsyncLifetime
{
    /// <summary>
    /// 本用例的数据库与宿主。
    /// </summary>
    private SqliteDatabase? _database;

    /// <summary>
    /// 已启动的宿主。
    /// </summary>
    private TestHost? _host;

    /// <summary>
    /// 启动宿主。
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _database = new SqliteDatabase();
        _host = await _database.StartHostAsync();
    }

    /// <summary>
    /// 停止宿主并删除数据库文件。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
            _database = null;
        }
    }

    /// <inheritdoc />
    protected override ILeaderElection CreateElection(TimeProvider clock)
    {
        var host = _host ?? throw new InvalidOperationException("The host has not been started.");
        return new DatabaseLeaderElection(host.Services, host.App, host.Services.GetRequiredService<SchedulerOptions>(), clock, host.Services.GetRequiredService<ILoggerFactory>());
    }

    /// <inheritdoc />
    protected override async Task<(ILeaderElection Competitor, IAsyncDisposable Host)> StartNodeAsync(TimeProvider clock, IFeature probe)
    {
        var database = new SqliteDatabase();
        try
        {
            // 竞争者是同一个库上另一个节点：先起它，被测节点启动、服务、排空期间它一直在服务，获取与释放经它自己的管道照常进行
            var rival = await database.StartHostAsync([new($"{SchedulerOptions.ConfigurationSection}:NodeId", "node-rival")], services => services.AddSingleton(clock));
            await database.StartHostAsync([new($"{SchedulerOptions.ConfigurationSection}:NodeId", "node-under-test")], services => services.AddSingleton(clock), b => b.UseFeature(probe));
            return (rival.Services.GetRequiredService<ILeaderElection>(), database);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }
}
