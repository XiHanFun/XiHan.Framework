// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 以 SQLite 跑库结构读取与实体目录的一致性套件。
/// </summary>
/// <remarks>
/// 运行时数据源指向同一个库文件。
/// </remarks>
public sealed class SqlSugarSchemaConformanceTests : SchemaConformance, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <inheritdoc />
    protected override bool ReadsSchema => true;

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => _database.Configuration;

    /// <inheritdoc />
    protected override IFeature CreateFeature() => SqliteDatabase.CreateFeature();

    /// <inheritdoc />
    protected override DataSourceDefinition CreateDefinition(string name) => new(name, DatabaseTypes.Sqlite, _database.ConnectionString);

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();
}
