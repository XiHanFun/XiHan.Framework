// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 以真实 PostgreSQL 跑一致性套件
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过。每个用例一个临时库。审计时间用例（新增后读回等于时钟、更新后创建时间不变）在这里才能暴露 Npgsql legacy 模式把 <c>timestamptz</c> 读成本地时间的问题：SQLite 存文本、按 UTC 解析，看不到。命名连接 <see cref="Leases"/> 指向同一个临时库（另一个连接、另一个事务；PostgreSQL 是行级锁，业务事务写过默认连接时它照样能提交），套件把 <c>ConformanceLease</c> 登记在上面；直接写入入口是 <see cref="SqlSugarUnitOfWork.EnsureTransactionAsync"/>
/// </remarks>
public sealed class PostgresRepositoryConformanceTests : RepositoryConformance, IDisposable
{
    /// <summary>
    /// 命名连接的名字
    /// </summary>
    private const string Leases = "leases";

    /// <summary>
    /// 本测试实例独占的临时库
    /// </summary>
    /// <remarks>
    /// 首次访问时建出（未设置环境变量时在此跳过用例）
    /// </remarks>
    private PostgresDatabase? _database;

    /// <summary>
    /// 临时库，按需创建
    /// </summary>
    private PostgresDatabase Database => _database ??= PostgresTestServer.CreateDatabase();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
        [.. Database.Configuration, new($"{SqlSugarPersistenceOptions.ConfigurationSection}:Connections:{Leases}", Database.ConnectionString)];

    /// <inheritdoc />
    protected override Func<IUnitOfWork, CancellationToken, ValueTask>? EnsureTransaction
        => static (unitOfWork, cancellationToken) => ((SqlSugarUnitOfWork)unitOfWork).EnsureTransactionAsync(cancellationToken);

    /// <inheritdoc />
    protected override string? NamedConnection => Leases;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        _ = Database;
        return PostgresDatabase.CreateFeature();
    }

    /// <summary>
    /// 测试结束后删库
    /// </summary>
    public void Dispose() => _database?.Dispose();
}
