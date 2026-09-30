// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Workflow.SqlSugar.Options;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 工作流存储测试夹具：一个库、真实的客户端解析器与工作单元管理器
/// </summary>
internal sealed class WorkflowTestDatabase : IDisposable
{
    /// <summary>
    /// 连接配置标识
    /// </summary>
    public const string ConfigId = "Default";

    private readonly ServiceProvider _serviceProvider;
    private readonly string? _sqliteFile;

    private WorkflowTestDatabase(DbType dbType, string connectionString, string? sqliteFile)
    {
        DbType = dbType;
        ConnectionString = connectionString;
        _sqliteFile = sqliteFile;

        Scope = new SqlSugarScope(
        [
            new ConnectionConfig
            {
                ConfigId = ConfigId,
                ConnectionString = connectionString,
                DbType = dbType,
                IsAutoCloseConnection = true
            }
        ]);
        Scope.GetConnectionScope(ConfigId).CodeFirst.InitTables(TestEntityTypes.All);

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        _serviceProvider = services.BuildServiceProvider();
        UnitOfWorkManager = _serviceProvider.GetRequiredService<IUnitOfWorkManager>();

        Resolver = new SqlSugarClientResolver(
            Scope,
            new FixedTenantConnectionResolver(ConfigId),
            new EntityModuleDataSourceResolver(),
            new StubModuleDataSourceConnectionResolver(),
            UnitOfWorkManager,
            new NoTenant(),
            new PassThroughConnectionConfigurator(),
            []);

        Executor = new WorkflowSqlSugarExecutor(
            Resolver,
            UnitOfWorkManager,
            Microsoft.Extensions.Options.Options.Create(new XiHanSqlSugarCoreOptions()),
            Microsoft.Extensions.Options.Options.Create(new XiHanWorkflowSqlSugarOptions()));
    }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DbType DbType { get; }

    /// <summary>
    /// 连接串
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// 多连接容器
    /// </summary>
    public SqlSugarScope Scope { get; }

    /// <summary>
    /// 工作单元管理器
    /// </summary>
    public IUnitOfWorkManager UnitOfWorkManager { get; }

    /// <summary>
    /// 客户端解析器
    /// </summary>
    public SqlSugarClientResolver Resolver { get; }

    /// <summary>
    /// 被测执行器
    /// </summary>
    public WorkflowSqlSugarExecutor Executor { get; }

    /// <summary>
    /// 创建临时 SQLite 库
    /// </summary>
    /// <returns>测试夹具</returns>
    public static WorkflowTestDatabase CreateSqlite()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_workflow_{Guid.NewGuid():N}.db");

        // 关闭连接池，用例结束后驱动不再持有临时库文件句柄
        return new WorkflowTestDatabase(DbType.Sqlite, $"DataSource={databaseFile};Pooling=False", databaseFile);
    }

    /// <summary>
    /// 连接真实 MySQL 库
    /// </summary>
    /// <param name="connectionString">连接串</param>
    /// <returns>测试夹具</returns>
    public static WorkflowTestDatabase CreateMySql(string connectionString)
    {
        return new WorkflowTestDatabase(DbType.MySql, connectionString, null);
    }

    /// <summary>
    /// 创建一条与夹具无关的新连接，只能看到已提交的数据
    /// </summary>
    /// <returns>探针客户端</returns>
    public SqlSugarClient CreateProbeClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConfigId = "Probe",
            ConnectionString = ConnectionString,
            DbType = DbType,
            IsAutoCloseConnection = true
        });
    }

    /// <summary>
    /// 释放连接并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        _serviceProvider.Dispose();
        Scope.Dispose();

        if (_sqliteFile is not null && File.Exists(_sqliteFile))
        {
            File.Delete(_sqliteFile);
        }
    }
}
