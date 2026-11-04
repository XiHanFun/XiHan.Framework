// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Workflow.Conformance.Storage;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库存储与租约锁对 PostgreSQL 通过存储套件
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过（<see cref="PostgresDatabase"/>）
/// </remarks>
public sealed class PostgresWorkflowStoreConformanceTests : WorkflowStoreConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowStoreHost> StartAsync()
    {
        var database = PostgresDatabase.Create();
        var host = await database.StartHostAsync();
        return new WorkflowStoreHost(host.Services, database);
    }
}
