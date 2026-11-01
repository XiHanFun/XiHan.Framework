// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 以 SQLite 跑运行时数据源的一致性套件。
/// </summary>
/// <remarks>
/// 应用库与外部库各一个临时文件；连不上的定义以只读模式指向不存在的目录。
/// </remarks>
public sealed class SqlSugarDataSourceConformanceTests : DataSourceConformance, IDisposable
{
    /// <summary>
    /// 应用自己的库。
    /// </summary>
    private readonly SqliteDatabase _application = new();

    /// <summary>
    /// 作为外部库的临时文件。
    /// </summary>
    private readonly SqliteDatabase _external = new();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => _application.Configuration;

    /// <inheritdoc />
    protected override IFeature CreateFeature() => SqliteDatabase.CreateFeature();

    /// <inheritdoc />
    protected override DataSourceDefinition CreateDefinition(string name) => new(name, DatabaseTypes.Sqlite, _external.ConnectionString);

    /// <inheritdoc />
    protected override DataSourceDefinition CreateUnreachableDefinition(string name)
        => new(name, DatabaseTypes.Sqlite, $"Data Source={Path.Combine(Path.GetTempPath(), $"xihan-missing-{Guid.NewGuid():N}", "x.db")};Mode=ReadOnly;Password=Conformance-Secret");

    /// <summary>
    /// 测试结束后删除两个临时库。
    /// </summary>
    public void Dispose()
    {
        // 套件「定义变了就替换」的用例在外部库的连接串后加了分号，是另外的连接池分组
        SqliteDatabase.ClearPool(_external.ConnectionString + ";");
        _application.Dispose();
        _external.Dispose();
    }
}
