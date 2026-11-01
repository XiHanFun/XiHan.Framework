// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using SqlSugar;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Security.Database.Grants;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 授予表的唯一索引（ADR-0072）。
/// </summary>
/// <remarks>
/// <c>InitTables</c> 在 SQLite 上建出四列唯一索引，同一授予的第二行被拒绝。
/// </remarks>
public sealed class PermissionGrantIndexTests : IDisposable
{
    /// <summary>
    /// 列出一张表上显式建出的索引：<c>名字 [unique] 列,列</c>，按名字排序。
    /// </summary>
    private const string IndexQuery = """
        SELECT m.name
            || CASE WHEN (SELECT l."unique" FROM pragma_index_list(m.tbl_name) AS l WHERE l.name = m.name) = 1 THEN ' unique ' ELSE ' ' END
            || (SELECT group_concat(i.name, ',') FROM (SELECT name FROM pragma_index_info(m.name) ORDER BY seqno) AS i)
        FROM sqlite_master AS m
        WHERE m.type = 'index' AND m.tbl_name = @table AND m.sql IS NOT NULL
        ORDER BY m.name
        """;

    /// <summary>
    /// 本测试独占的临时数据库文件。
    /// </summary>
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xihan-security-index-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 清空本库的连接池后删除数据库文件。
    /// </summary>
    /// <remarks>
    /// 只清本库连接串的连接池（连接池按连接串分组）。不用 <c>SqliteConnection.ClearAllPools()</c>：它清整个进程的连接池，会把并行用例此刻正在打开的连接当作泄漏回收并关掉句柄，那个用例随即以 <c>ObjectDisposedException</c>（<c>SQLitePCL.sqlite3</c>）失败。
    /// </remarks>
    public void Dispose()
    {
        using (var connection = new SqliteConnection($"Data Source={_path}"))
        {
            SqliteConnection.ClearPool(connection);
        }

        File.Delete(_path);
    }

    [Fact]
    public void InitTables_CreatesTheUniqueGrantIndex_ThatRejectsADuplicateGrant()
    {
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={_path}", IsAutoCloseConnection = true }));
        client.CodeFirst.InitTables<PermissionGrantRecord>();

        Assert.Equal(
            ["UX_PermissionGrantRecord_SuKi_SuId_Pe_TeId unique SubjectKind,SubjectId,Permission,TenantId"],
            client.Ado.SqlQuery<string>(IndexQuery, new { table = nameof(PermissionGrantRecord) }));

        client.Insertable(Grant(PermissionGrantRecord.GlobalTenantId)).ExecuteCommand();
        client.Insertable(Grant("acme")).ExecuteCommand();
        // 全局授予的租户列是空字符串而不是 NULL，唯一索引同样约束它
        Assert.ThrowsAny<Exception>(() => client.Insertable(Grant(PermissionGrantRecord.GlobalTenantId)).ExecuteCommand());
        Assert.ThrowsAny<Exception>(() => client.Insertable(Grant("acme")).ExecuteCommand());
    }

    /// <summary>
    /// 角色 editor 的 orders.write 授予。
    /// </summary>
    private static PermissionGrantRecord Grant(string tenantId)
        => new() { Id = Guid.CreateVersion7(), SubjectKind = "role", SubjectId = "editor", Permission = "orders.write", TenantId = tenantId };
}
