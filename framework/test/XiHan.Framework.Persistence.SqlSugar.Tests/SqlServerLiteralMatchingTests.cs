// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SQL Server 上字符串按字面匹配
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_SQLSERVER</c>，未设置即跳过。每个用例一个临时库（服务器缺省排序规则 <c>SQL_Latin1_General_CP1_CI_AS</c>：<c>LIKE</c> 不区分大小写）。SQL Server 的 <c>LIKE</c> 没有缺省转义符，另把 <c>[</c> 当字符类的开头，约定一并转义
/// </remarks>
public sealed class SqlServerLiteralMatchingTests : SqlSugarLiteralMatchingTests, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时库
    /// </summary>
    /// <remarks>
    /// 首次访问时建出（未设置环境变量时在此跳过用例）
    /// </remarks>
    private SqlServerDatabase? _database;

    /// <summary>
    /// 临时库，按需创建
    /// </summary>
    private SqlServerDatabase Database => _database ??= SqlServerTestServer.CreateDatabase();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => Database.Configuration;

    /// <inheritdoc />
    protected override bool IgnoresCase => true;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        _ = Database;
        return SqlServerDatabase.CreateFeature();
    }

    /// <summary>
    /// 测试结束后断开连接池并删库
    /// </summary>
    public void Dispose() => _database?.Dispose();
}
