// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SQLite 上字符串按字面匹配
/// </summary>
/// <remarks>
/// 每个用例一个临时数据库文件。SQLite 的 <c>LIKE</c> 只对 ASCII 字母不区分大小写，没有缺省转义符
/// </remarks>
public sealed class SqliteLiteralMatchingTests : SqlSugarLiteralMatchingTests, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => _database.Configuration;

    /// <inheritdoc />
    protected override bool IgnoresCase => true;

    /// <inheritdoc />
    protected override IFeature CreateFeature() => SqliteDatabase.CreateFeature();

    /// <summary>
    /// 测试结束后删除临时数据库文件
    /// </summary>
    public void Dispose() => _database.Dispose();
}
