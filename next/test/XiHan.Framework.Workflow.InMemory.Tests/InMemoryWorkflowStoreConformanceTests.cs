// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Conformance.Storage;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 内存存储与进程内锁通过存储套件。
/// </summary>
/// <remarks>
/// 宿主只装配内存工作流特性：存储是单例，在套件开的调用里按分区读写；锁等待上限 0.2 秒。
/// </remarks>
public sealed class InMemoryWorkflowStoreConformanceTests : WorkflowStoreConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowStoreHost> StartAsync()
    {
        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryWorkflowFeature>(), [new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "0.2"), new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")]);
        return new WorkflowStoreHost(host.Services, host);
    }
}
