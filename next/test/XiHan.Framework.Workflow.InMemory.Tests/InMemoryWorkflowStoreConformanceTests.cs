// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Conformance.Storage;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 内存存储与实例锁通过存储套件。
/// </summary>
public sealed class InMemoryWorkflowStoreConformanceTests : WorkflowStoreConformance
{
    /// <inheritdoc />
    protected override Task<WorkflowStoreHost> StartAsync()
    {
        var options = new InMemoryWorkflowOptions();
        var instanceLock = new InMemoryWorkflowInstanceLock(new WorkflowOptions { LockTimeout = TimeSpan.FromMilliseconds(200) });
        return Task.FromResult(new WorkflowStoreHost(new InMemoryWorkflowDefinitionStore(options), new InMemoryWorkflowInstanceStore(options), new InMemoryWorkflowBookmarkStore(options), instanceLock, new Disposer(instanceLock)));
    }

    /// <summary>
    /// 释放锁。
    /// </summary>
    private sealed class Disposer(InMemoryWorkflowInstanceLock instanceLock) : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            instanceLock.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
