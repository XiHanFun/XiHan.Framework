// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Tasks;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 扫描到期书签前先调用钩子的书签存储装饰器。
/// </summary>
/// <remarks>
/// 钩子收到被扫描的分区（空为平台）；用例借它在轮询周期的某个分区扫描进行中切换应用状态。其余调用原样转给 Provider 的存储。
/// </remarks>
internal sealed class ScanHookBookmarkStore(IWorkflowBookmarkStore inner, Func<string?, ValueTask> onDue) : IWorkflowBookmarkStore
{
    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> DueAsync(string? tenantId, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        await onDue(tenantId).ConfigureAwait(false);
        return await inner.DueAsync(tenantId, now, limit, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowBookmark>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken) => inner.FindAsync(tenantId, id, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken) => inner.FindManyAsync(tenantId, ids, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByInstanceAsync(string? tenantId, string instanceId, CancellationToken cancellationToken) => inner.ByInstanceAsync(tenantId, instanceId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByKindAndKeyAsync(string? tenantId, string kind, string key, CancellationToken cancellationToken) => inner.ByKindAndKeyAsync(tenantId, kind, key, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ListUserTasksAsync(string? tenantId, UserTaskQuery query, CancellationToken cancellationToken) => inner.ListUserTasksAsync(tenantId, query, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<long>> CountUserTasksAsync(string? tenantId, UserTaskQuery query, CancellationToken cancellationToken) => inner.CountUserTasksAsync(tenantId, query, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> AvailableAsync(string? tenantId, string kind, string key, DateTimeOffset now, int limit, CancellationToken cancellationToken) => inner.AvailableAsync(tenantId, kind, key, now, limit, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> BySignalAsync(string? tenantId, string signalName, string? correlationId, CancellationToken cancellationToken) => inner.BySignalAsync(tenantId, signalName, correlationId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken) => inner.SaveAsync(tenantId, bookmarks, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken) => inner.DeleteAsync(tenantId, ids, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> DeleteByInstanceAsync(string? tenantId, IReadOnlyList<string> instanceIds, CancellationToken cancellationToken) => inner.DeleteByInstanceAsync(tenantId, instanceIds, cancellationToken);
}
