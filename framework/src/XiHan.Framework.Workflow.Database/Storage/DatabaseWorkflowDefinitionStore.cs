// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 数据库定义存储（作用域服务）
/// </summary>
/// <remarks>
/// 在调用方的工作单元里经仓储读写，随它提交或回滚。分区为空（平台）读写 <see cref="PlatformWorkflowDefinitionRecord"/>（平台库），否则读写 <see cref="WorkflowDefinitionRecord"/>（实现 <c>IMultiTenant</c>，随租户分库）；分区必须等于工作单元的租户。失败 → <see cref="WorkflowErrors.StoreUnavailable"/>（只读拒绝原样返回）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    /// <summary>
    /// 调用方的工作单元
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 平台分区的表
    /// </summary>
    private readonly DefinitionRows<PlatformWorkflowDefinitionRecord> _platform;

    /// <summary>
    /// 租户分区的表
    /// </summary>
    private readonly DefinitionRows<WorkflowDefinitionRecord> _tenant;

    /// <summary>
    /// 创建（作用域服务）
    /// </summary>
    public DatabaseWorkflowDefinitionStore(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _unitOfWork = services.GetRequiredService<IUnitOfWork>();
        _platform = new DefinitionRows<PlatformWorkflowDefinitionRecord>(services.GetRequiredService<IRepository<PlatformWorkflowDefinitionRecord, string>>());
        _tenant = new DefinitionRows<WorkflowDefinitionRecord>(services.GetRequiredService<IRepository<WorkflowDefinitionRecord, string>>());
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindAsync(id, cancellationToken).ConfigureAwait(false) : await _tenant.FindAsync(id, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindManyAsync(ids, cancellationToken).ConfigureAwait(false) : await _tenant.FindManyAsync(ids, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string? tenantId, WorkflowName name, int version, CancellationToken cancellationToken)
    {
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindAsync(name, version, cancellationToken).ConfigureAwait(false) : await _tenant.FindAsync(name, version, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(string? tenantId, WorkflowName name, CancellationToken cancellationToken)
    {
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindLatestPublishedAsync(name, cancellationToken).ConfigureAwait(false) : await _tenant.FindLatestPublishedAsync(name, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<int>> MaxVersionAsync(string? tenantId, WorkflowName name, CancellationToken cancellationToken)
    {
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.MaxVersionAsync(name, cancellationToken).ConfigureAwait(false) : await _tenant.MaxVersionAsync(name, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(string? tenantId, WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken)
    {
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.ListAsync(name, status, cancellationToken).ConfigureAwait(false) : await _tenant.ListAsync(name, status, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinitionSummary>>> ListSummariesAsync(string? tenantId, WorkflowDefinitionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.ListSummariesAsync(query, cancellationToken).ConfigureAwait(false) : await _tenant.ListSummariesAsync(query, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<long>> CountAsync(string? tenantId, WorkflowDefinitionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.CountAsync(query, cancellationToken).ConfigureAwait(false) : await _tenant.CountAsync(query, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        Enter(tenantId);
        foreach (var definition in definitions)
        {
            StorePartition.EnsureOwned(tenantId, definition.TenantId, "definition", definition.Id);
        }

        return StorePartition.OrUnavailable(tenantId is null ? await _platform.SaveAsync(definitions, cancellationToken).ConfigureAwait(false) : await _tenant.SaveAsync(definitions, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.DeleteAsync(ids, cancellationToken).ConfigureAwait(false) : await _tenant.DeleteAsync(ids, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 核对分区与工作单元的租户一致
    /// </summary>
    private void Enter(string? tenantId) => StorePartition.Ensure(_unitOfWork, tenantId);
}
