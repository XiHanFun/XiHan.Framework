// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 配了从库的 SqlSugar Provider 跑一致性套件
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c> 与 <c>XIHAN_TEST_POSTGRES_REPLICA</c>，未设置即跳过。仓储的约定（每个作用域一份配置副本）、写入的语义在读写分离下都不能变；主库同步回放，只读调用读从库也看得到已提交的数据
/// </remarks>
public sealed class PostgresReplicaConformanceTests : RepositoryConformance, IDisposable
{
    /// <summary>
    /// 命名连接的名字
    /// </summary>
    /// <remarks>
    /// 与默认连接同一个库，共用从库
    /// </remarks>
    private const string Leases = "leases";

    /// <summary>
    /// 本测试实例独占的临时库
    /// </summary>
    private PostgresDatabase? _database;

    /// <summary>
    /// 临时库，按需创建
    /// </summary>
    private PostgresDatabase Database
    {
        get
        {
            PostgresReplicaServer.RequireConfiguration();
            return _database ??= PostgresTestServer.CreateDatabase();
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
        [.. Database.Configuration, new($"{SqlSugarPersistenceOptions.ConfigurationSection}:Connections:{Leases}", Database.ConnectionString)];

    /// <inheritdoc />
    protected override string? NamedConnection => Leases;

    /// <inheritdoc />
    protected override Func<IUnitOfWork, CancellationToken, ValueTask>? EnsureTransaction
        => static (unitOfWork, cancellationToken) => ((SqlSugarUnitOfWork)unitOfWork).EnsureTransactionAsync(cancellationToken);

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        var replica = PostgresReplicaServer.ConnectionStringAsync(Database).GetAwaiter().GetResult();
        return new SqlSugarPersistenceFeature((binding, config) =>
        {
            config.DbType = DbType.PostgreSQL;
            config.ConnectionString = binding.ValueOrThrow().Value;
            config.SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = replica, HitRate = 10 }];
        });
    }

    /// <summary>
    /// 测试结束后删库
    /// </summary>
    public void Dispose() => _database?.Dispose();
}
