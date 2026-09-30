// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.ProviderContractTests;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.SqlSugar.BackgroundJobs;
using XiHan.Framework.Tasks.SqlSugar.Clients;
using XiHan.Framework.Tasks.SqlSugar.Entities;
using XiHan.Framework.Tasks.SqlSugar.Options;

namespace XiHan.Framework.Tasks.SqlSugar.Tests.Contracts;

/// <summary>
/// SqlSugar 后台作业存储的契约夹具，每个客户端各用一个数据库连接与一个存储实例，共用一个可控时钟；建立时与释放时清空本夹具使用的后台作业表
/// </summary>
internal sealed class SqlSugarBackgroundJobContractFixture : IProviderContractFixture<IBackgroundJobStore>
{
    private const ProviderCapabilities SharedCapabilities =
        ProviderCapabilities.Persistence
        | ProviderCapabilities.ExclusiveClaim
        | ProviderCapabilities.ClaimExpiry
        | ProviderCapabilities.ControllableTime;

    private static readonly TimeSpan LeaseTimeout = TimeSpan.FromMinutes(5);

    private readonly string _connectionString;
    private readonly DbType _dbType;
    private readonly string? _databaseFile;
    private readonly ManualClock _clock;
    private readonly SqlSugarClient _adminClient;
    private readonly List<IDisposable> _resources = [];
    private readonly Lock _resourcesLock = new();

    private SqlSugarBackgroundJobContractFixture(
        string connectionString,
        DbType dbType,
        ProviderCapabilities capabilities,
        DateTime utcNow,
        string? databaseFile)
    {
        _connectionString = connectionString;
        _dbType = dbType;
        _databaseFile = databaseFile;
        _clock = new ManualClock(utcNow);
        Capabilities = capabilities;

        _adminClient = NewClient();
        try
        {
            _adminClient.CodeFirst.InitTables(typeof(SysBackgroundJob));
            _adminClient.Deleteable<SysBackgroundJob>().ExecuteCommand();
        }
        catch
        {
            _adminClient.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 提供方声明的能力
    /// </summary>
    public ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// 可控时钟的当前时间
    /// </summary>
    public DateTime UtcNow => _clock.Now;

    /// <summary>
    /// 创建基于临时 SQLite 库的夹具
    /// </summary>
    /// <returns>夹具</returns>
    public static SqlSugarBackgroundJobContractFixture CreateSqlite()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_tasks_contract_{Guid.NewGuid():N}.db");
        return new SqlSugarBackgroundJobContractFixture(
            $"DataSource={databaseFile};Pooling=False",
            DbType.Sqlite,
            SharedCapabilities,
            new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            databaseFile);
    }

    /// <summary>
    /// 创建基于真实 MySQL 的夹具，时钟从当前整秒开始
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>夹具</returns>
    public static SqlSugarBackgroundJobContractFixture CreateMySql(string connectionString)
    {
        var now = DateTime.UtcNow;
        return new SqlSugarBackgroundJobContractFixture(
            connectionString,
            DbType.MySql,
            SharedCapabilities | ProviderCapabilities.ConcurrentStorage,
            new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc),
            null);
    }

    /// <summary>
    /// 创建一个使用独立连接的后台作业存储
    /// </summary>
    /// <returns>后台作业存储</returns>
    public Task<IBackgroundJobStore> CreateClientAsync()
    {
        var client = NewClient();
        var currentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
        var resolver = new StubClientResolver(client, currentTenant);

        var services = new ServiceCollection();
        services.AddScoped<ISqlSugarClientResolver>(_ => resolver);
        var provider = services.BuildServiceProvider();

        lock (_resourcesLock)
        {
            _resources.Add(provider);
            _resources.Add(client);
        }

        var accessor = new TasksHostClientAccessor(provider.GetRequiredService<IServiceScopeFactory>(), currentTenant);

        IBackgroundJobStore store = new SqlSugarBackgroundJobStore(
            accessor,
            _clock,
            Microsoft.Extensions.Options.Options.Create(new XiHanTasksSqlSugarOptions
            {
                BackgroundJobLeaseTimeout = LeaseTimeout
            }));

        return Task.FromResult(store);
    }

    /// <summary>
    /// 推进可控时钟
    /// </summary>
    /// <param name="duration">推进的时长</param>
    public void AdvanceTime(TimeSpan duration)
    {
        _clock.Advance(duration);
    }

    /// <summary>
    /// 把时钟推进到超过租约时长，使全部领取过期
    /// </summary>
    /// <returns>已完成的任务</returns>
    public Task ExpireClaimsAsync()
    {
        _clock.Advance(LeaseTimeout + TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 清空本夹具使用的后台作业表并释放连接，SQLite 同时删除临时库文件
    /// </summary>
    /// <returns>任务</returns>
    public async ValueTask DisposeAsync()
    {
        await _adminClient.Deleteable<SysBackgroundJob>().ExecuteCommandAsync();

        lock (_resourcesLock)
        {
            foreach (var resource in _resources)
            {
                resource.Dispose();
            }

            _resources.Clear();
        }

        _adminClient.Dispose();

        if (_databaseFile is not null && File.Exists(_databaseFile))
        {
            File.Delete(_databaseFile);
        }
    }

    private SqlSugarClient NewClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = _connectionString,
            DbType = _dbType,
            IsAutoCloseConnection = true
        });
    }
}
