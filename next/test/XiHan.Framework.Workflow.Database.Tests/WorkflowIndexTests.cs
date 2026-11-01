// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using SqlSugar;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 工作流表的索引（ADR-0072）。
/// </summary>
/// <remarks>
/// <c>InitTables</c> 在 SQLite 上建出定义、实例、节点实例与书签的索引，租约表只有主键。
/// 定义的名字 + 租户 + 版本唯一；全局定义的租户列是 NULL，SQLite（与 PostgreSQL、MySQL 一样）的唯一索引不约束含 NULL 的行。
/// </remarks>
public sealed class WorkflowIndexTests : IDisposable
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
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xihan-workflow-index-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 清空连接池后删除数据库文件。
    /// </summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public void InitTables_CreatesTheQueryIndexes_AndTheDefinitionKeyIsUniqueWithinATenant()
    {
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={_path}", IsAutoCloseConnection = true }));
        client.CodeFirst.InitTables(typeof(WorkflowDefinitionRecord), typeof(WorkflowInstanceRecord), typeof(WorkflowNodeInstanceRecord), typeof(WorkflowBookmarkRecord), typeof(WorkflowLockRecord));

        Assert.Equal(
            ["UX_WorkflowDefinitionRecord_Na_TeId_Ve unique Name,TenantId,Version"],
            Indexes(client, nameof(WorkflowDefinitionRecord)));
        Assert.Equal(
            [
                "IX_WorkflowInstanceRecord_CoId CorrelationId",
                "IX_WorkflowInstanceRecord_CrAt CreatedAt",
                "IX_WorkflowInstanceRecord_EnAt EndedAt",
                "IX_WorkflowInstanceRecord_PaInId ParentInstanceId",
            ],
            Indexes(client, nameof(WorkflowInstanceRecord)));
        Assert.Equal(["IX_WorkflowNodeInstanceRecord_InId InstanceId"], Indexes(client, nameof(WorkflowNodeInstanceRecord)));
        Assert.Equal(
            ["IX_WorkflowBookmarkRecord_DuAt DueAt", "IX_WorkflowBookmarkRecord_InId InstanceId", "IX_WorkflowBookmarkRecord_Ki_Ke Kind,Key"],
            Indexes(client, nameof(WorkflowBookmarkRecord)));
        Assert.Empty(Indexes(client, nameof(WorkflowLockRecord)));

        client.Insertable(Definition("t1")).ExecuteCommand();
        Assert.ThrowsAny<Exception>(() => client.Insertable(Definition("t1")).ExecuteCommand());
        // 全局定义（租户为 NULL）不受唯一索引约束：版本号只靠目录按最大版本加一分配
        client.Insertable(Definition(null)).ExecuteCommand();
        client.Insertable(Definition(null)).ExecuteCommand();
        Assert.Equal(3, client.Queryable<WorkflowDefinitionRecord>().Count());
    }

    /// <summary>
    /// 一张表上的索引。
    /// </summary>
    private static List<string> Indexes(SqlSugarClient client, string table) => client.Ado.SqlQuery<string>(IndexQuery, new { table });

    /// <summary>
    /// 名字 approval 的第 1 版定义，标识每次新取。
    /// </summary>
    private static WorkflowDefinitionRecord Definition(string? tenantId)
        => new() { Id = Guid.CreateVersion7().ToString("N"), Name = "approval", Version = 1, Status = "Draft", TenantId = tenantId, Document = "{}" };
}
