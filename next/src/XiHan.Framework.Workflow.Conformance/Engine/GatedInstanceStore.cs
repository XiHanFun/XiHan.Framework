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
    public ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string id, CancellationToken cancellationToken) => inner.FindAsync(id, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken) => inner.FindManyAsync(ids, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await probe.OnActiveQueryAsync(query).ConfigureAwait(false);
        return await inner.ListAsync(query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<Result<long>> CountAsync(WorkflowInstanceQuery query, CancellationToken cancellationToken) => inner.CountAsync(query, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowInstance> instances, CancellationToken cancellationToken) => inner.SaveAsync(instances, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken) => inner.DeleteAsync(ids, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string id, CancellationToken cancellationToken) => inner.FindNodeInstanceAsync(id, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string instanceId, CancellationToken cancellationToken) => inner.NodeInstancesAsync(instanceId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> SaveNodeInstancesAsync(IReadOnlyList<WorkflowNodeInstance> nodeInstances, CancellationToken cancellationToken) => inner.SaveNodeInstancesAsync(nodeInstances, cancellationToken);
}
