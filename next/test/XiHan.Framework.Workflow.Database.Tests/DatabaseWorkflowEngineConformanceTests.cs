// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库工作流特性对 SQLite 通过引擎套件。
/// </summary>
/// <remarks>
/// 后台轮询周期设得很长，由用例主动 <c>PollAsync</c>。
/// </remarks>
public sealed class DatabaseWorkflowEngineConformanceTests : WorkflowEngineConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "2"),
        };
        if (configuration is not null)
        {
            settings.AddRange(configuration);
        }

        var database = new SqliteDatabase();
        var host = await database.StartHostAsync(settings, configure);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), database);
    }
}
