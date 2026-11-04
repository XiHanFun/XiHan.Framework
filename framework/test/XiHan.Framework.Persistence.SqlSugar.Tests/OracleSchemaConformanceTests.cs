// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 以真实 Oracle 跑库结构读取与实体目录的一致性套件
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_ORACLE</c>，未设置即跳过。运行时数据源指向同一个临时用户（schema）
/// </remarks>
public sealed class OracleSchemaConformanceTests : SchemaConformance, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时用户
    /// </summary>
    /// <remarks>
    /// 首次访问时建出（未设置环境变量时在此跳过用例）
    /// </remarks>
    private OracleSchema? _database;

    /// <summary>
    /// 临时用户，按需创建
    /// </summary>
    private OracleSchema Database => _database ??= OracleTestServer.CreateSchema();

    /// <inheritdoc />
    protected override bool ReadsSchema => true;

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => Database.Configuration;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        _ = Database;
        return OracleSchema.CreateFeature();
    }

    /// <inheritdoc />
    protected override DataSourceDefinition CreateDefinition(string name) => new(name, DatabaseTypes.Oracle, Database.ConnectionString);

    /// <summary>
    /// 测试结束后断开连接池并删用户
    /// </summary>
    public void Dispose() => _database?.Dispose();
}
