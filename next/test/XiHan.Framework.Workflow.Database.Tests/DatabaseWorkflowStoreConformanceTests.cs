// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Workflow.Conformance.Storage;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库存储与租约锁对 SQLite 通过存储套件。
/// </summary>
/// <remarks>
/// 存储在套件开的调用里、调用方的工作单元里读写；平台分区落平台表，租户分区落实现 <c>IMultiTenant</c> 的租户表。
/// </remarks>
public sealed class DatabaseWorkflowStoreConformanceTests : WorkflowStoreConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowStoreHost> StartAsync()
    {
        var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        return new WorkflowStoreHost(host.Services, database);
    }
}
