// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// EF Core Provider 跑运行时数据源的一致性套件
/// </summary>
/// <remarks>
/// 不支持：取用与测试连接都返回明确的错误码
/// </remarks>
public sealed class EntityFrameworkCoreDataSourceConformanceTests : DataSourceConformance, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => _database.Configuration;

    /// <inheritdoc />
    protected override IFeature CreateFeature() => SqliteDatabase.CreateFeature();

    /// <inheritdoc />
    protected override DataSourceDefinition? CreateDefinition(string name) => null;

    /// <inheritdoc />
    protected override DataSourceDefinition? CreateUnreachableDefinition(string name) => null;

    /// <summary>
    /// 测试结束后删除临时数据库文件
    /// </summary>
    public void Dispose() => _database.Dispose();
}
