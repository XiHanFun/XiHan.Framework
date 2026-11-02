// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using SqlSugar;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 工作流表的索引（ADR-0072，ADR-0077、ADR-0096 修订）。
/// </summary>
/// <remarks>
/// <c>InitTables</c> 在 SQLite 上建出租户分区与平台分区的定义、实例、节点实例与书签的索引，租约表只有主键。
/// 租户定义的名字 + 租户 + 版本唯一（租户列不为空，每一行都受约束）；平台定义在自己的表里，名字 + 版本唯一，没有可为空的列。
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
    public void InitTables_CreatesTheQueryIndexes_AndTheDefinitionKeysAreUniqueInEveryPartition()
    {
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={_path}", IsAutoCloseConnection = true }));
        client.CodeFirst.InitTables(
            typeof(WorkflowDefinitionRecord), typeof(WorkflowInstanceRecord), typeof(WorkflowNodeInstanceRecord), typeof(WorkflowBookmarkRecord),
            typeof(PlatformWorkflowDefinitionRecord), typeof(PlatformWorkflowInstanceRecord), typeof(PlatformWorkflowNodeInstanceRecord), typeof(PlatformWorkflowBookmarkRecord),
            typeof(WorkflowLockRecord));
        Assert.Equal(["UX_WorkflowDefinitionRecord_Na_TeId_Ve unique Name,TenantId,Version"], Indexes(client, nameof(WorkflowDefinitionRecord)));
        Assert.Equal(["UX_PlatformWorkflowDefinitionRecord_Na_Ve unique Name,Version"], Indexes(client, nameof(PlatformWorkflowDefinitionRecord)));
        Assert.Equal(
            [
                "IX_WorkflowInstanceRecord_CoId CorrelationId",
                "IX_WorkflowInstanceRecord_PaInId ParentInstanceId",
                "IX_WorkflowInstanceRecord_TeId_CrAt TenantId,CreatedAt",
                "IX_WorkflowInstanceRecord_TeId_EnAt TenantId,EndedAt",
                "IX_WorkflowInstanceRecord_TeId_StId_CrAt TenantId,StarterId,CreatedAt",
            ],
            Indexes(client, nameof(WorkflowInstanceRecord)));
        Assert.Equal(
            [
                "IX_PlatformWorkflowInstanceRecord_CoId CorrelationId",
                "IX_PlatformWorkflowInstanceRecord_CrAt CreatedAt",
                "IX_PlatformWorkflowInstanceRecord_EnAt EndedAt",
                "IX_PlatformWorkflowInstanceRecord_PaInId ParentInstanceId",
                "IX_PlatformWorkflowInstanceRecord_StId_CrAt StarterId,CreatedAt",
            ],
            Indexes(client, nameof(PlatformWorkflowInstanceRecord)));
        Assert.Equal(["IX_WorkflowNodeInstanceRecord_InId InstanceId"], Indexes(client, nameof(WorkflowNodeInstanceRecord)));
        Assert.Equal(["IX_PlatformWorkflowNodeInstanceRecord_InId InstanceId"], Indexes(client, nameof(PlatformWorkflowNodeInstanceRecord)));
        Assert.Equal(
            ["IX_WorkflowBookmarkRecord_InId InstanceId", "IX_WorkflowBookmarkRecord_Ki_Ke Kind,Key", "IX_WorkflowBookmarkRecord_TeId_DuAt TenantId,DueAt"],
            Indexes(client, nameof(WorkflowBookmarkRecord)));
        Assert.Equal(
            ["IX_PlatformWorkflowBookmarkRecord_DuAt DueAt", "IX_PlatformWorkflowBookmarkRecord_InId InstanceId", "IX_PlatformWorkflowBookmarkRecord_Ki_Ke Kind,Key"],
            Indexes(client, nameof(PlatformWorkflowBookmarkRecord)));
        Assert.Empty(Indexes(client, nameof(WorkflowLockRecord)));

        // 租户定义：同一租户的名字 + 版本只能有一行，别的租户同名同版本不冲突
        client.Insertable(TenantDefinition("t1")).ExecuteCommand();
        Assert.ThrowsAny<Exception>(() => client.Insertable(TenantDefinition("t1")).ExecuteCommand());
        client.Insertable(TenantDefinition("t2")).ExecuteCommand();

        // 平台定义：没有可为空的租户列，名字 + 版本照样受唯一索引约束（ADR-0072 里全局定义不受约束的缺口随之关闭）
        client.Insertable(PlatformDefinition()).ExecuteCommand();
        Assert.ThrowsAny<Exception>(() => client.Insertable(PlatformDefinition()).ExecuteCommand());
        Assert.Equal(2, client.Queryable<WorkflowDefinitionRecord>().Count());
        Assert.Equal(1, client.Queryable<PlatformWorkflowDefinitionRecord>().Count());
    }

    /// <summary>
    /// 一张表上的索引。
    /// </summary>
    private static List<string> Indexes(SqlSugarClient client, string table) => client.Ado.SqlQuery<string>(IndexQuery, new { table });

    /// <summary>
    /// 某个租户里名字 approval 的第 1 版定义，标识每次新取。
    /// </summary>
    private static WorkflowDefinitionRecord TenantDefinition(string tenantId)
        => new() { Id = Guid.CreateVersion7().ToString("N"), Name = "approval", Version = 1, Status = "Draft", TenantId = tenantId, Document = "{}" };

    /// <summary>
    /// 平台里名字 approval 的第 1 版定义，标识每次新取。
    /// </summary>
    private static PlatformWorkflowDefinitionRecord PlatformDefinition()
        => new() { Id = Guid.CreateVersion7().ToString("N"), Name = "approval", Version = 1, Status = "Draft", Document = "{}" };
}
