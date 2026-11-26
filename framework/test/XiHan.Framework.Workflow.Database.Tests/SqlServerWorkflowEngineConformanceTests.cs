// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库工作流特性对 SQL Server 通过引擎套件
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_SQLSERVER</c>，未设置即跳过（<see cref="SqlServerDatabase"/>：读已提交快照，调用方事务没提交的写入不挡别的调用读），由 CI 的 database 分片运行。后台轮询周期设得很长，由用例主动 <c>PollAsync</c>
/// </remarks>
public sealed class SqlServerWorkflowEngineConformanceTests : WorkflowEngineConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var database = SqlServerDatabase.Create();
        var host = await database.StartHostAsync(configuration, configure);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), database);
    }
}
