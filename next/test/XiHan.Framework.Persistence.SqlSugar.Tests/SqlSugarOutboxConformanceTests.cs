// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 发件箱一致性套件以 SqlSugar 跑在 SQLite 上。
/// </summary>
/// <remarks>
/// 每个用例一个临时数据库文件，开着 <c>InitTables</c> 建出发件箱表。
/// </remarks>
public sealed class SqlSugarOutboxConformanceTests : OutboxConformance, IDisposable
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
