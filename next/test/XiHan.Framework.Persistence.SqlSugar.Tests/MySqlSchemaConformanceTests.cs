// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 以真实 MySQL 跑库结构读取与实体目录的一致性套件。
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置即跳过。运行时数据源指向同一个临时库。
/// </remarks>
public sealed class MySqlSchemaConformanceTests : SchemaConformance, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时库。
    /// </summary>
    /// <remarks>
    /// 首次访问时建出（未设置环境变量时在此跳过用例）。
    /// </remarks>
    private MySqlDatabase? _database;

    /// <summary>
    /// 临时库，按需创建。
    /// </summary>
    private MySqlDatabase Database => _database ??= MySqlTestServer.CreateDatabase();

    /// <inheritdoc />
    protected override bool ReadsSchema => true;

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => Database.Configuration;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        _ = Database;
        return MySqlDatabase.CreateFeature();
    }

    /// <inheritdoc />
    protected override DataSourceDefinition CreateDefinition(string name) => new(name, DatabaseTypes.MySql, Database.ConnectionString);

    /// <summary>
    /// 测试结束后断开连接池并删库。
    /// </summary>
    public void Dispose() => _database?.Dispose();
}
