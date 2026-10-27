// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 以真实 PostgreSQL 跑一致性套件（环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过）；每个用例一个临时库。
/// 审计时间用例（新增后读回等于时钟、更新后创建时间不变）在这里才能暴露 Npgsql legacy 模式把 <c>timestamptz</c> 读成本地时间的问题：SQLite 存文本、按 UTC 解析，看不到。
/// </summary>
public sealed class PostgresRepositoryConformanceTests : RepositoryConformance, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时库；首次访问时建出（未设置环境变量时在此跳过用例）。
    /// </summary>
    private PostgresDatabase? _database;

    /// <summary>
    /// 临时库，按需创建。
    /// </summary>
    private PostgresDatabase Database => _database ??= PostgresTestServer.CreateDatabase();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => Database.Configuration;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        _ = Database;
        return PostgresDatabase.CreateFeature();
    }

    /// <summary>
    /// 测试结束后删库。
    /// </summary>
    public void Dispose() => _database?.Dispose();
}
