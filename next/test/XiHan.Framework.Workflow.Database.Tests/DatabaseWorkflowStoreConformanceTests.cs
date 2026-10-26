// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Conformance.Storage;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库存储与租约式实例锁对 SQLite 通过存储套件。
/// </summary>
public sealed class DatabaseWorkflowStoreConformanceTests : WorkflowStoreConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowStoreHost> StartAsync()
    {
        var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        return new WorkflowStoreHost(
            host.Services.GetRequiredService<IWorkflowDefinitionStore>(),
            host.Services.GetRequiredService<IWorkflowInstanceStore>(),
            host.Services.GetRequiredService<IWorkflowBookmarkStore>(),
            host.Services.GetRequiredService<IWorkflowInstanceLock>(),
            database);
    }
}
