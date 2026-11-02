// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// PostgreSQL 上字符串按字面匹配。
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过。每个用例一个临时库。PostgreSQL 的 <c>LIKE</c> 区分大小写，缺省以反斜杠转义——约定写明 <c>ESCAPE '!'</c>，反斜杠按字面。
/// </remarks>
public sealed class PostgresLiteralMatchingTests : SqlSugarLiteralMatchingTests, IDisposable
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
    protected override bool IgnoresCase => false;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        _ = Database;
        return PostgresDatabase.CreateFeature();
    }

    [Fact]
    public async Task StandardConformingStringsOff_StillMatchesLiterally()
    {
        // standard_conforming_strings = off 时普通字符串字面量里的反斜杠是转义符：约定生成的 SQL 里没有反斜杠，结果与缺省设置相同。
        // 建表与写入在缺省设置下做：SqlSugar 的多行写入把值内联成字符串字面量、不转义反斜杠，off 下含反斜杠的值让写入的 SQL 不合法（与查询的翻译无关）；
        // 会话里切换设置后在同一连接上查询
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = Database.ConnectionString, IsAutoCloseConnection = true }));
        SqlSugarConventions.InitTables(client, typeof(LiteralMatchRecord));
        await client.Insertable(new List<LiteralMatchRecord>
        {
            new() { Id = Guid.CreateVersion7(), Name = "a\\b", Fragment = "\\" },
            new() { Id = Guid.CreateVersion7(), Name = "ab", Fragment = "\\" },
            new() { Id = Guid.CreateVersion7(), Name = "budget_2026", Fragment = "t_2" },
            new() { Id = Guid.CreateVersion7(), Name = "budgetx2026", Fragment = "t_2" },
        }).ExecuteCommandAsync();
        using (client.Ado.OpenAlways())
        {
            await client.Ado.ExecuteCommandAsync("SET standard_conforming_strings = off");
            Assert.Equal("off", await client.Ado.GetStringAsync("SHOW standard_conforming_strings"));
            var backslash = "a\\b";
            var underscore = "t_2";

            Assert.Equal(["a\\b"], await client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(backslash)).Select(static r => r.Name).ToListAsync());
            Assert.Equal(["budget_2026"], await client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(underscore)).Select(static r => r.Name).ToListAsync());
            Assert.Equal(["a\\b", "budget_2026"], (await client.Queryable<LiteralMatchRecord>().Where(static r => r.Name.Contains(r.Fragment)).Select(static r => r.Name).ToListAsync()).Order(StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// 测试结束后删库。
    /// </summary>
    public void Dispose() => _database?.Dispose();
}
