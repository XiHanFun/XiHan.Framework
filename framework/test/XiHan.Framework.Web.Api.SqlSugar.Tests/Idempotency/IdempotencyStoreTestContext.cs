// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Web.Api.Idempotency;
using XiHan.Framework.Web.Api.SqlSugar.Entities;
using XiHan.Framework.Web.Api.SqlSugar.Idempotency;

namespace XiHan.Framework.Web.Api.SqlSugar.Tests.Idempotency;

/// <summary>
/// SqlSugar 幂等存储测试夹具：临时 SQLite 文件库、最小工作单元容器与可推进时钟
/// </summary>
internal sealed class IdempotencyStoreTestContext : IDisposable
{
    private const int CommandTimeoutSeconds = 10;

    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_idem_store_{Guid.NewGuid():N}.db");

    /// <summary>
    /// 构造函数
    /// </summary>
    public IdempotencyStoreTestContext()
    {
        Provider = BuildUnitOfWorkProvider();

        Client = CreateClient();
        Client.CodeFirst.InitTables(typeof(SysIdempotencyRecord));
    }

    /// <summary>
    /// 幂等配置
    /// </summary>
    public XiHanIdempotencyOptions Options { get; } = new();

    /// <summary>
    /// 可推进的时钟
    /// </summary>
    public ManualTimeProvider Clock { get; } = new();

    /// <summary>
    /// 工作单元服务提供者
    /// </summary>
    public ServiceProvider Provider { get; }

    /// <summary>
    /// 默认客户端
    /// </summary>
    public SqlSugarClient Client { get; }

    /// <summary>
    /// 对同一个库文件新建一个客户端
    /// </summary>
    /// <returns>客户端</returns>
    public SqlSugarClient CreateClient()
    {
        var client = new SqlSugarClient(CreateConnectionConfig());
        client.Ado.CommandTimeOut = CommandTimeoutSeconds;
        return client;
    }

    /// <summary>
    /// 对同一个库文件新建一个线程安全的客户端，模拟另一个进程
    /// </summary>
    /// <returns>客户端</returns>
    public SqlSugarScope CreateScopeClient()
    {
        return new SqlSugarScope(CreateConnectionConfig());
    }

    /// <summary>
    /// 创建使用默认客户端的存储
    /// </summary>
    /// <returns>存储</returns>
    public SqlSugarIdempotencyStore CreateStore()
    {
        return CreateStore(Client);
    }

    /// <summary>
    /// 创建使用指定客户端的存储
    /// </summary>
    /// <param name="client">客户端</param>
    /// <returns>存储</returns>
    public SqlSugarIdempotencyStore CreateStore(ISqlSugarClient client)
    {
        return new SqlSugarIdempotencyStore(
            new StubClientResolver(client),
            Provider.GetRequiredService<IUnitOfWorkManager>(),
            Microsoft.Extensions.Options.Options.Create(Options),
            Clock);
    }

    /// <summary>
    /// 构建只含工作单元服务的最小容器
    /// </summary>
    /// <returns>服务提供者</returns>
    public static ServiceProvider BuildUnitOfWorkProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<XiHanUnitOfWorkDefaultOptions>();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddSingleton<IUnitOfWorkTransactionBehaviourProvider, NullUnitOfWorkTransactionBehaviourProvider>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 按记录键读取记录
    /// </summary>
    /// <param name="key">记录键</param>
    /// <returns>记录，不存在时为 null</returns>
    public SysIdempotencyRecord? FindRecord(IdempotencyRecordKey key)
    {
        var keyHash = key.ComputeHash();
        return Client.Queryable<SysIdempotencyRecord>().Where(record => record.KeyHash == keyHash).First();
    }

    /// <summary>
    /// 释放服务提供者与临时库文件
    /// </summary>
    public void Dispose()
    {
        Client.Dispose();
        Provider.Dispose();
        try
        {
            File.Delete(_databaseFile);
        }
        catch (IOException)
        {
        }
    }

    private ConnectionConfig CreateConnectionConfig()
    {
        return new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        };
    }
}

/// <summary>
/// 一律返回同一个客户端的解析器
/// </summary>
/// <param name="client">客户端</param>
internal sealed class StubClientResolver(ISqlSugarClient client) : ISqlSugarClientResolver
{
    /// <inheritdoc />
    public ISqlSugarClient GetCurrentClient()
    {
        return client;
    }

    /// <inheritdoc />
    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        return client;
    }

    /// <inheritdoc />
    public ISqlSugarClient GetClient(string configId)
    {
        return client;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return ["0"];
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetCurrentLayoutConfigIds()
    {
        return ["0"];
    }

    /// <inheritdoc />
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        return [client];
    }

    /// <inheritdoc />
    public ITenant AsTenant()
    {
        return client.AsTenant();
    }
}
