// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using SqlSugar;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Scheduling.Database.Entities;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 调度表的索引（ADR-0072）。
/// </summary>
/// <remarks>
/// <c>InitTables</c> 在 SQLite 上建出触发器与运行记录的索引，作业、节点、租约表只有主键。
/// 触发器的作业 + 名字唯一：同一作业的同名触发器换个标识也写不进第二行。
/// </remarks>
public sealed class SchedulingIndexTests : IDisposable
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
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xihan-scheduling-index-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 清空连接池后删除数据库文件。
    /// </summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public void InitTables_CreatesTriggerAndRunIndexes_AndTheTriggerKeyIsUnique()
    {
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={_path}", IsAutoCloseConnection = true }));
        client.CodeFirst.InitTables(typeof(JobEntity), typeof(TriggerEntity), typeof(JobRunEntity), typeof(SchedulerNodeEntity), typeof(SchedulerLeaseEntity));

        Assert.Equal(
            ["IX_TriggerEntity_St_Lo_NeFiAt State,Local,NextFireAt", "UX_TriggerEntity_Jo_Na unique Job,Name"],
            Indexes(client, nameof(TriggerEntity)));
        Assert.Equal(
            [
                "IX_JobRunEntity_CoAt CompletedAt",
                "IX_JobRunEntity_Jo_CoAt Job,CompletedAt",
                "IX_JobRunEntity_ScAt ScheduledAt",
                "IX_JobRunEntity_St_NoId State,NodeId",
            ],
            Indexes(client, nameof(JobRunEntity)));
        Assert.Empty(Indexes(client, nameof(JobEntity)));
        Assert.Empty(Indexes(client, nameof(SchedulerNodeEntity)));
        Assert.Empty(Indexes(client, nameof(SchedulerLeaseEntity)));

        client.Insertable(Trigger("nightly/daily")).ExecuteCommand();
        Assert.ThrowsAny<Exception>(() => client.Insertable(Trigger("nightly/daily-copy")).ExecuteCommand());
    }

    /// <summary>
    /// 一张表上的索引。
    /// </summary>
    private static List<string> Indexes(SqlSugarClient client, string table) => client.Ado.SqlQuery<string>(IndexQuery, new { table });

    /// <summary>
    /// 作业 nightly 的触发器 daily，标识由调用方给。
    /// </summary>
    private static TriggerEntity Trigger(string id)
        => new() { Id = id, Job = "nightly", Name = "daily", Schedule = "cron:0 0 * * *", ConcurrencyStamp = Guid.NewGuid().ToString("N") };
}
