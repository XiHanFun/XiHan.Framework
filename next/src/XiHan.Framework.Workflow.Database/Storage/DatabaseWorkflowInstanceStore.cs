// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 数据库实例与节点实例存储（作用域服务）。
/// </summary>
/// <remarks>
/// 在调用方的工作单元里经仓储读写，随它提交或回滚。分区为空（平台）读写 <see cref="PlatformWorkflowInstanceRecord"/> 与 <see cref="PlatformWorkflowNodeInstanceRecord"/>，否则读写 <see cref="WorkflowInstanceRecord"/> 与 <see cref="WorkflowNodeInstanceRecord"/>（随租户分库）；分区必须等于工作单元的租户。失败 → <see cref="WorkflowErrors.StoreUnavailable"/>（只读拒绝原样返回）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseWorkflowInstanceStore : IWorkflowInstanceStore
{
    /// <summary>
    /// 调用方的工作单元。
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 平台分区的表。
    /// </summary>
    private readonly InstanceRows<PlatformWorkflowInstanceRecord, PlatformWorkflowNodeInstanceRecord> _platform;

    /// <summary>
    /// 租户分区的表。
    /// </summary>
    private readonly InstanceRows<WorkflowInstanceRecord, WorkflowNodeInstanceRecord> _tenant;

    /// <summary>
    /// 创建（作用域服务）。
    /// </summary>
    public DatabaseWorkflowInstanceStore(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var sequence = services.GetRequiredService<WriteSequence>();
        _platform = new InstanceRows<PlatformWorkflowInstanceRecord, PlatformWorkflowNodeInstanceRecord>(
            services.GetRequiredService<IRepository<PlatformWorkflowInstanceRecord, string>>(),
            services.GetRequiredService<IRepository<PlatformWorkflowNodeInstanceRecord, string>>(),
            sequence);
        _tenant = new InstanceRows<WorkflowInstanceRecord, WorkflowNodeInstanceRecord>(
            services.GetRequiredService<IRepository<WorkflowInstanceRecord, string>>(),
            services.GetRequiredService<IRepository<WorkflowNodeInstanceRecord, string>>(),
            sequence);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowInstance>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindAsync(id, cancellationToken).ConfigureAwait(false) : await _tenant.FindAsync(id, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowInstance>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindManyAsync(ids, cancellationToken).ConfigureAwait(false) : await _tenant.FindManyAsync(ids, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowInstance>>> ListAsync(string? tenantId, WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.ListAsync(query, cancellationToken).ConfigureAwait(false) : await _tenant.ListAsync(query, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<long>> CountAsync(string? tenantId, WorkflowInstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.CountAsync(query, cancellationToken).ConfigureAwait(false) : await _tenant.CountAsync(query, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowInstance> instances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instances);
        Enter(tenantId);
        foreach (var instance in instances)
        {
            StorePartition.EnsureOwned(tenantId, instance.TenantId, "instance", instance.Id);
        }

        return StorePartition.OrUnavailable(tenantId is null ? await _platform.SaveAsync(instances, cancellationToken).ConfigureAwait(false) : await _tenant.SaveAsync(instances, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.DeleteAsync(ids, cancellationToken).ConfigureAwait(false) : await _tenant.DeleteAsync(ids, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowNodeInstance>>> FindNodeInstanceAsync(string? tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindNodeInstanceAsync(id, cancellationToken).ConfigureAwait(false) : await _tenant.FindNodeInstanceAsync(id, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowNodeInstance>>> NodeInstancesAsync(string? tenantId, string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.NodeInstancesAsync(instanceId, cancellationToken).ConfigureAwait(false) : await _tenant.NodeInstancesAsync(instanceId, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveNodeInstancesAsync(string? tenantId, IReadOnlyList<WorkflowNodeInstance> nodeInstances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeInstances);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.SaveNodeInstancesAsync(nodeInstances, cancellationToken).ConfigureAwait(false) : await _tenant.SaveNodeInstancesAsync(nodeInstances, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 核对分区与工作单元的租户一致。
    /// </summary>
    private void Enter(string? tenantId) => StorePartition.Ensure(_unitOfWork, tenantId);
}
