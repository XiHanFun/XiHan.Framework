// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库工作流在 EF Core（SQLite）上跑任务一致性套件
/// </summary>
/// <remarks>
/// 租约表在命名连接的另一个库文件
/// </remarks>
public sealed class EntityFrameworkCoreWorkflowTaskConformanceTests : WorkflowTaskConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var database = new EntityFrameworkCoreSqliteDatabase();
        var host = await database.StartHostAsync(configuration, configure);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), database);
    }
}
