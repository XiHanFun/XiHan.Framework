// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// MySQL 上字符串按字面匹配
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置即跳过。每个用例一个临时库（<c>utf8mb4</c>，缺省排序规则 <c>utf8mb4_0900_ai_ci</c>：<c>LIKE</c> 不区分大小写与重音）。MySQL 的 <c>LIKE</c> 缺省以反斜杠转义——约定写明 <c>ESCAPE '!'</c>，反斜杠按字面；<c>NO_BACKSLASH_ESCAPES</c> 模式下同一条 SQL 照样成立
/// </remarks>
public sealed class MySqlLiteralMatchingTests : SqlSugarLiteralMatchingTests, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时库
    /// </summary>
    /// <remarks>
    /// 首次访问时建出（未设置环境变量时在此跳过用例）
    /// </remarks>
    private MySqlDatabase? _database;

    /// <summary>
    /// 临时库，按需创建
    /// </summary>
    private MySqlDatabase Database => _database ??= MySqlTestServer.CreateDatabase();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => Database.Configuration;

    /// <inheritdoc />
    protected override bool IgnoresCase => true;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        _ = Database;
        return MySqlDatabase.CreateFeature();
    }

    [Fact]
    public async Task NoBackslashEscapesMode_StillMatchesLiterally()
    {
        // 这个模式下字符串字面量里的反斜杠是普通字符，LIKE 也不再有缺省转义符：约定生成的 SQL 里没有反斜杠、转义符由 ESCAPE '!' 写明，结果与缺省模式相同
        // 驱动（MySqlConnector）不探测服务器的模式，要在连接串里声明 NoBackslashEscapes=true，它才按这个模式给参数加引号；会话里先切换模式，再在同一连接上建表、写入与查询
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.MySql, ConnectionString = $"{Database.ConnectionString};NoBackslashEscapes=true", IsAutoCloseConnection = true }));
        using (client.Ado.OpenAlways())
        {
            await client.Ado.ExecuteCommandAsync("SET SESSION sql_mode = CONCAT(@@SESSION.sql_mode, ',NO_BACKSLASH_ESCAPES')");
            Assert.Contains("NO_BACKSLASH_ESCAPES", await client.Ado.GetStringAsync("SELECT @@SESSION.sql_mode"), StringComparison.Ordinal);
            SqlSugarConventions.InitTables(client, typeof(LiteralMatchRecord));
            await client.Insertable(new List<LiteralMatchRecord>
            {
                new() { Id = Guid.CreateVersion7(), Name = "a\\b", Fragment = "\\" },
                new() { Id = Guid.CreateVersion7(), Name = "ab", Fragment = "\\" },
                new() { Id = Guid.CreateVersion7(), Name = "50% off", Fragment = "0%" },
                new() { Id = Guid.CreateVersion7(), Name = "500 off", Fragment = "0%" },
            }).ExecuteCommandAsync();
            var backslash = "a\\b";
            var percent = "0%";

            Assert.Equal(["a\\b"], await client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(backslash)).Select(static r => r.Name).ToListAsync());
            Assert.Equal(["50% off"], await client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(percent)).Select(static r => r.Name).ToListAsync());
            Assert.Equal(["50% off", "a\\b"], (await client.Queryable<LiteralMatchRecord>().Where(static r => r.Name.Contains(r.Fragment)).Select(static r => r.Name).ToListAsync()).Order(StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// 测试结束后断开连接池并删库
    /// </summary>
    public void Dispose() => _database?.Dispose();
}
