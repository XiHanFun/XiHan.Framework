// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar Provider 以 SQLite 跑一致性套件；每个用例一个临时数据库文件。
/// </summary>
public sealed class SqlSugarRepositoryConformanceTests : RepositoryConformance, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => _database.Configuration;

    /// <inheritdoc />
    protected override IFeature CreateFeature() => SqliteDatabase.CreateFeature();

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();
}
