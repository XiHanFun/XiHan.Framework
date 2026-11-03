// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 发件箱一致性套件以 SqlSugar 跑在真实 PostgreSQL 上。
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过。每个用例一个临时库；尝试次数、下一次投递时间与死信时间经 <c>timestamptz</c> 往返后逐轮比对。
/// </remarks>
public sealed class PostgresOutboxConformanceTests : OutboxConformance, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时库。
    /// </summary>
    /// <remarks>
    /// 首次访问时建出（未设置环境变量时在此跳过用例）。
    /// </remarks>
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
