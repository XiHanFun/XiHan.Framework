// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 在被标记的启动查未结束实例时停在探针闸门前的实例存储装饰器。
/// </summary>
/// <remarks>
/// 其余调用原样转给 Provider 的存储。
/// </remarks>
internal sealed class GatedInstanceStore(IWorkflowInstanceStore inner, StartRaceProbe probe) : IWorkflowInstanceStore
{
    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken) => inner.FindAsync(tenantId, id, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken) => inner.FindManyAsync(tenantId, ids, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(string? tenantId, WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await probe.OnActiveQueryAsync(query).ConfigureAwait(false);
        return await inner.ListAsync(tenantId, query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<Result<long>> CountAsync(string? tenantId, WorkflowInstanceQuery query, CancellationToken cancellationToken) => inner.CountAsync(tenantId, query, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowInstance> instances, CancellationToken cancellationToken) => inner.SaveAsync(tenantId, instances, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken) => inner.DeleteAsync(tenantId, ids, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string? tenantId, string id, CancellationToken cancellationToken) => inner.FindNodeInstanceAsync(tenantId, id, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string? tenantId, string instanceId, CancellationToken cancellationToken) => inner.NodeInstancesAsync(tenantId, instanceId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> SaveNodeInstancesAsync(string? tenantId, IReadOnlyList<WorkflowNodeInstance> nodeInstances, CancellationToken cancellationToken) => inner.SaveNodeInstancesAsync(tenantId, nodeInstances, cancellationToken);
}
