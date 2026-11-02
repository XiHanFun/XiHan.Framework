// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 装了发件箱的宿主上，内存工作流特性照样通过用户任务与外部任务套件。
/// </summary>
/// <remarks>
/// 见 <see cref="InMemoryWorkflowEngineConformanceWithOutboxTests"/>。
/// </remarks>
public sealed class InMemoryWorkflowTaskConformanceWithOutboxTests : WorkflowTaskConformance
{
    /// <inheritdoc />
    protected override Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
        => OutboxHost.StartAsync(configure, configuration);
}
