// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Options;
using XiHan.Framework.EventBus.SqlSugar.Outbox;
using XiHan.Framework.ProviderContractTests;

namespace XiHan.Framework.EventBus.SqlSugar.Tests.Contracts;

/// <summary>
/// SqlSugar 发件箱的契约夹具，每个客户端各用一个数据库连接与一个发件箱实例；建立时与释放时清空本夹具使用的发件箱表
/// </summary>
internal sealed class SqlSugarOutboxContractFixture : IProviderContractFixture<IEventOutbox>
{
    private const ProviderCapabilities SharedCapabilities =
        ProviderCapabilities.Persistence | ProviderCapabilities.ExclusiveClaim | ProviderCapabilities.ClaimExpiry;

    private readonly string _connectionString;
    private readonly DbType _dbType;
    private readonly string? _databaseFile;
    private readonly SqlSugarClient _adminClient;
    private readonly List<SqlSugarClient> _clients = [];
    private readonly Lock _clientsLock = new();

    private SqlSugarOutboxContractFixture(string connectionString, DbType dbType, ProviderCapabilities capabilities, string? databaseFile)
    {
        _connectionString = connectionString;
        _dbType = dbType;
        _databaseFile = databaseFile;
        Capabilities = capabilities;

        _adminClient = NewClient();
        try
        {
            _adminClient.CodeFirst.InitTables(typeof(SysEventOutbox));
            _adminClient.Deleteable<SysEventOutbox>().ExecuteCommand();
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
    /// 发件箱以系统时间判断领取超时
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// 创建基于临时 SQLite 库的夹具
    /// </summary>
    /// <returns>夹具</returns>
    public static SqlSugarOutboxContractFixture CreateSqlite()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_outbox_contract_{Guid.NewGuid():N}.db");
        return new SqlSugarOutboxContractFixture($"DataSource={databaseFile};Pooling=False", DbType.Sqlite, SharedCapabilities, databaseFile);
    }

    /// <summary>
    /// 创建基于真实 MySQL 的夹具
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>夹具</returns>
    public static SqlSugarOutboxContractFixture CreateMySql(string connectionString)
    {
        return new SqlSugarOutboxContractFixture(connectionString, DbType.MySql, SharedCapabilities | ProviderCapabilities.ConcurrentStorage, null);
    }

    /// <summary>
    /// 创建一个使用独立连接的发件箱
    /// </summary>
    /// <returns>发件箱</returns>
    public Task<IEventOutbox> CreateClientAsync()
    {
        var client = NewClient();
        lock (_clientsLock)
        {
            _clients.Add(client);
        }

        var clients = new Dictionary<string, SqlSugarClient>(StringComparer.Ordinal)
        {
            [OutboxTestContext.MainConfigId] = client
        };
        var resolver = new StubClientResolver(clients, [OutboxTestContext.MainConfigId], OutboxTestContext.MainConfigId);

        IEventOutbox outbox = new SqlSugarEventOutbox(
            resolver,
            new FakeCurrentTenant(),
            Microsoft.Extensions.Options.Options.Create(new XiHanSqlSugarEventBoxOptions
            {
                ClaimTimeout = TimeSpan.FromMinutes(5)
            }),
            NullLogger<SqlSugarEventOutbox>.Instance);

        return Task.FromResult(outbox);
    }

    /// <summary>
    /// 不支持
    /// </summary>
    /// <param name="duration">推进的时长</param>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public void AdvanceTime(TimeSpan duration)
    {
        throw new NotSupportedException("SqlSugar 发件箱以系统时间判断领取超时，夹具不支持推进时间。");
    }

    /// <summary>
    /// 把全部领取时间改到一天前，使其超过领取超时
    /// </summary>
    /// <returns>任务</returns>
    public async Task ExpireClaimsAsync()
    {
        var stale = DateTimeOffset.UtcNow.AddDays(-1);
        await _adminClient.Updateable<SysEventOutbox>()
            .SetColumns(item => new SysEventOutbox { ClaimTime = stale })
            .Where(item => item.ClaimTime != null)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 清空本夹具使用的发件箱表并释放连接，SQLite 同时删除临时库文件
    /// </summary>
    /// <returns>任务</returns>
    public async ValueTask DisposeAsync()
    {
        await _adminClient.Deleteable<SysEventOutbox>().ExecuteCommandAsync();

        lock (_clientsLock)
        {
            foreach (var client in _clients)
            {
                client.Dispose();
            }

            _clients.Clear();
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
