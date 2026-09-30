// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Options;
using XiHan.Framework.EventBus.SqlSugar.Outbox;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 发件箱测试夹具，提供一个或两个临时 SQLite 库与发件箱实例
/// </summary>
internal sealed class OutboxTestContext : IDisposable
{
    /// <summary>
    /// 主库的连接配置标识
    /// </summary>
    public const string MainConfigId = "Default";

    /// <summary>
    /// 模块库的连接配置标识
    /// </summary>
    public const string ModuleConfigId = "Default_Shop";

    private readonly List<string> _databaseFiles = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="claimTimeout">领取超时</param>
    /// <param name="withModuleDatabase">是否额外创建一个模块库</param>
    /// <param name="moduleSharesMainDatabase">模块库连接标识是否与主库指向同一个客户端（不额外建库）</param>
    public OutboxTestContext(
        TimeSpan? claimTimeout = null,
        bool withModuleDatabase = false,
        bool moduleSharesMainDatabase = false)
    {
        List<string> configIds = withModuleDatabase || moduleSharesMainDatabase
            ? [MainConfigId, ModuleConfigId]
            : [MainConfigId];

        foreach (var configId in configIds)
        {
            if (moduleSharesMainDatabase && configId == ModuleConfigId)
            {
                Clients[configId] = Clients[MainConfigId];
                continue;
            }

            var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_outbox_{Guid.NewGuid():N}.db");
            _databaseFiles.Add(databaseFile);

            var client = new SqlSugarClient(new ConnectionConfig
            {
                // 禁用连接池
                ConnectionString = $"DataSource={databaseFile};Pooling=False",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            client.CodeFirst.InitTables(typeof(SysEventOutbox));
            Clients[configId] = client;
        }

        Resolver = new StubClientResolver(Clients, configIds, MainConfigId);

        CurrentTenant = new FakeCurrentTenant();

        Outbox = new SqlSugarEventOutbox(
            Resolver,
            CurrentTenant,
            Microsoft.Extensions.Options.Options.Create(new XiHanSqlSugarEventBoxOptions
            {
                ClaimTimeout = claimTimeout ?? TimeSpan.FromMinutes(5)
            }),
            NullLogger<SqlSugarEventOutbox>.Instance);
    }

    /// <summary>
    /// 各库的客户端
    /// </summary>
    public Dictionary<string, SqlSugarClient> Clients { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 可编程的客户端解析器
    /// </summary>
    public StubClientResolver Resolver { get; }

    /// <summary>
    /// 当前租户
    /// </summary>
    public FakeCurrentTenant CurrentTenant { get; }

    /// <summary>
    /// 被测发件箱
    /// </summary>
    public SqlSugarEventOutbox Outbox { get; }

    /// <summary>
    /// 主库客户端
    /// </summary>
    public SqlSugarClient Client
    {
        get { return Clients[MainConfigId]; }
    }

    /// <summary>
    /// 模块库客户端
    /// </summary>
    public SqlSugarClient ModuleClient
    {
        get { return Clients[ModuleConfigId]; }
    }

    /// <summary>
    /// 释放客户端并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        // 模块库可能与主库共用同一个客户端实例，去重后再释放，避免重复 Dispose
        foreach (var client in Clients.Values.Distinct())
        {
            client.Dispose();
        }

        foreach (var databaseFile in _databaseFiles)
        {
            if (File.Exists(databaseFile))
            {
                File.Delete(databaseFile);
            }
        }
    }
}

/// <summary>
/// 测试用客户端解析器，按连接配置标识返回对应客户端
/// </summary>
internal sealed class StubClientResolver : ISqlSugarClientResolver
{
    private readonly IReadOnlyDictionary<string, SqlSugarClient> _clients;
    private readonly IReadOnlyList<string> _configIds;
    private readonly string _currentConfigId;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clients">各库的客户端</param>
    /// <param name="configIds">连接配置标识，主库在前</param>
    /// <param name="currentConfigId">当前库的连接配置标识</param>
    public StubClientResolver(
        IReadOnlyDictionary<string, SqlSugarClient> clients,
        IReadOnlyList<string> configIds,
        string currentConfigId)
    {
        _clients = clients;
        _configIds = configIds;
        _currentConfigId = currentConfigId;
    }

    /// <summary>
    /// 当前工作单元已登记的连接配置标识，用例可直接增删
    /// </summary>
    public List<string> EnlistedConfigIds { get; } = [];

    /// <summary>
    /// 取客户端时抛出的异常，用例据此模拟库不可达
    /// </summary>
    public Dictionary<string, Exception> FaultyConfigIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 当前库的选择器，为空时使用构造函数传入的当前库
    /// </summary>
    public Func<string>? CurrentConfigIdSelector { get; set; }

    /// <summary>
    /// 当前布局的连接配置标识选择器，为空时使用构造函数传入的连接配置标识
    /// </summary>
    public Func<IReadOnlyList<string>>? CurrentLayoutSelector { get; set; }

    /// <summary>
    /// 获取当前客户端
    /// </summary>
    /// <returns>当前库的客户端</returns>
    public ISqlSugarClient GetCurrentClient()
    {
        return GetClient(CurrentConfigIdSelector?.Invoke() ?? _currentConfigId);
    }

    /// <summary>
    /// 获取实体对应的客户端
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>当前库的客户端</returns>
    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        return GetClient(_currentConfigId);
    }

    /// <summary>
    /// 按连接配置标识获取客户端
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <returns>该库的客户端</returns>
    public ISqlSugarClient GetClient(string configId)
    {
        if (FaultyConfigIds.TryGetValue(configId, out var error))
        {
            throw error;
        }

        return _clients[configId];
    }

    /// <summary>
    /// 获取当前工作单元已登记的连接配置标识
    /// </summary>
    /// <returns>已登记的连接配置标识</returns>
    public IReadOnlyList<string> GetEnlistedConfigIds()
    {
        return EnlistedConfigIds;
    }

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    /// <returns>连接配置标识集合</returns>
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return _configIds;
    }

    /// <summary>
    /// 获取当前布局的全部连接配置标识
    /// </summary>
    /// <returns>连接配置标识集合</returns>
    public IReadOnlyList<string> GetCurrentLayoutConfigIds()
    {
        return CurrentLayoutSelector?.Invoke() ?? _configIds;
    }

    /// <summary>
    /// 获取所有库的客户端
    /// </summary>
    /// <returns>客户端集合</returns>
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        return _clients.Values;
    }

    /// <summary>
    /// 获取底层多租户接口
    /// </summary>
    /// <returns>不返回，始终抛出</returns>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public ITenant AsTenant()
    {
        throw new NotSupportedException("测试桩不支持多租户切换。");
    }
}
