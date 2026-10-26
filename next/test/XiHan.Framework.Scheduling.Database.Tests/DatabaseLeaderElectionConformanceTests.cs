// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Conformance;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 让 <see cref="Leadership.DatabaseLeaderElection"/>（对着 SQLite）跑完整套 <see cref="LeaderElectionConformance"/> 用例；套件不释放宿主，所以每个用例在生命周期钩子里启动与释放自己的宿主。
/// </summary>
public sealed class DatabaseLeaderElectionConformanceTests : LeaderElectionConformance, IAsyncLifetime
{
    /// <summary>
    /// 本用例的数据库与宿主。
    /// </summary>
    private SqliteDatabase? _database;

    /// <summary>
    /// 已启动的宿主里的选举。
    /// </summary>
    private ILeaderElection? _election;

    /// <summary>
    /// 启动宿主。
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _database = new SqliteDatabase();
        var host = await _database.StartHostAsync();
        _election = host.Services.GetRequiredService<ILeaderElection>();
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
    protected override ILeaderElection CreateElection()
        => _election ?? throw new InvalidOperationException("The host has not been started.");
}
