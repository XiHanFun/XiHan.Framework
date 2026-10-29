// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 内存工作流特性通过用户任务与外部任务套件。
/// </summary>
/// <remarks>
/// 轮询周期设得很长，由用例主动 <c>PollAsync</c>。
/// </remarks>
public sealed class InMemoryWorkflowTaskConformanceTests : WorkflowTaskConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
            new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "2"),
        };
        if (configuration is not null)
        {
            settings.AddRange(configuration);
        }

        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryWorkflowFeature>(), settings, configure);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), host);
    }
}
