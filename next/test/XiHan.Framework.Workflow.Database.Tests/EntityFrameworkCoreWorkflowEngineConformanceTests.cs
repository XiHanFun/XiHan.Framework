// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库工作流在 EF Core（SQLite）上跑引擎一致性套件。
/// </summary>
/// <remarks>
/// 租约表在命名连接的另一个库文件。
/// 套件里加入调用方事务的用例在业务事务写过之后取实例锁：租约表与业务同一个库文件时，租约的写入要等调用方自己的事务结束，忙等超时后失败（ADR-0087）。
/// </remarks>
public sealed class EntityFrameworkCoreWorkflowEngineConformanceTests : WorkflowEngineConformance
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
