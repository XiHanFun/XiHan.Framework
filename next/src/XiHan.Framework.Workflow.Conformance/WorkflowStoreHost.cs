// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Conformance;

/// <summary>
/// 存储套件的夹具：被测的三个存储与实例锁，以及释放它们的句柄。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowStoreHost : IAsyncDisposable
{
    /// <summary>
    /// 释放句柄。
    /// </summary>
    private readonly IAsyncDisposable _host;

    /// <summary>
    /// 创建。
    /// </summary>
    public WorkflowStoreHost(IWorkflowDefinitionStore definitions, IWorkflowInstanceStore instances, IWorkflowBookmarkStore bookmarks, IWorkflowInstanceLock instanceLock, IAsyncDisposable host)
    {
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        Instances = instances ?? throw new ArgumentNullException(nameof(instances));
        Bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        Lock = instanceLock ?? throw new ArgumentNullException(nameof(instanceLock));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// 定义存储。
    /// </summary>
    public IWorkflowDefinitionStore Definitions { get; }

    /// <summary>
    /// 实例存储。
    /// </summary>
    public IWorkflowInstanceStore Instances { get; }

    /// <summary>
    /// 书签存储。
    /// </summary>
    public IWorkflowBookmarkStore Bookmarks { get; }

    /// <summary>
    /// 实例锁。
    /// </summary>
    public IWorkflowInstanceLock Lock { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
