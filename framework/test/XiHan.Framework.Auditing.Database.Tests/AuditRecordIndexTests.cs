// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using SqlSugar;
using XiHan.Framework.Auditing.Database.Trail;
using XiHan.Framework.Persistence.SqlSugar;

namespace XiHan.Framework.Auditing.Database.Tests;

/// <summary>
/// 审计表的索引（ADR-0072）
/// </summary>
/// <remarks>
/// <c>InitTables</c> 在 SQLite 上建出序号、时间、trace-id 与租户 + 序号四个普通索引
/// 写入序号不唯一：不同节点的序号只按时钟大致有序，可能相同，两行同号都能写入
/// </remarks>
public sealed class AuditRecordIndexTests : IDisposable
{
    /// <summary>
    /// 列出一张表上显式建出的索引：<c>名字 [unique] 列,列</c>，按名字排序
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
    /// 本测试独占的临时数据库文件
    /// </summary>
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xihan-auditing-index-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 清空本库的连接池后删除数据库文件
    /// </summary>
    /// <remarks>
    /// 只清本库连接串的连接池（连接池按连接串分组）。不用 <c>SqliteConnection.ClearAllPools()</c>：它清整个进程的连接池，会把并行用例此刻正在打开的连接当作泄漏回收并关掉句柄，那个用例随即以 <c>ObjectDisposedException</c>（<c>SQLitePCL.sqlite3</c>）失败
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
    public void InitTables_CreatesTheQueryIndexes_AndSequenceStaysNonUnique()
    {
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={_path}", IsAutoCloseConnection = true }));
        client.CodeFirst.InitTables<AuditRecord>();

        Assert.Equal(
            [
                "IX_AuditRecord_Se Sequence",
                "IX_AuditRecord_TeId_Se TenantId,Sequence",
                "IX_AuditRecord_Ti Timestamp",
                "IX_AuditRecord_TrId TraceId",
            ],
            client.Ado.SqlQuery<string>(IndexQuery, new { table = nameof(AuditRecord) }));

        client.Insertable(Record(42)).ExecuteCommand();
        client.Insertable(Record(42)).ExecuteCommand();
        Assert.Equal(2, client.Queryable<AuditRecord>().Count());
    }

    /// <summary>
    /// 一条成功的审计记录
    /// </summary>
    private static AuditRecord Record(long sequence)
        => new() { Id = Guid.CreateVersion7(), Sequence = sequence, Timestamp = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero), Action = "orders.create", Succeeded = true, TraceId = "0af7651916cd43dd8448eb211c80319c" };
}
