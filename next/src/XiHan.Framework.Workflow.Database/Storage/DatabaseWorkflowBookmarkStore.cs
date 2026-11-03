// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 数据库书签存储（作用域服务）
/// </summary>
/// <remarks>
/// 在调用方的工作单元里经仓储读写，随它提交或回滚。分区为空（平台）读写 <see cref="PlatformWorkflowBookmarkRecord"/>，否则读写 <see cref="WorkflowBookmarkRecord"/>（随租户分库）；分区必须等于工作单元的租户。失败 → <see cref="WorkflowErrors.StoreUnavailable"/>（只读拒绝原样返回）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseWorkflowBookmarkStore : IWorkflowBookmarkStore
{
    /// <summary>
    /// 调用方的工作单元
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 平台分区的表
    /// </summary>
    private readonly BookmarkRows<PlatformWorkflowBookmarkRecord, PlatformWorkflowInstanceRecord> _platform;

    /// <summary>
    /// 租户分区的表
    /// </summary>
    private readonly BookmarkRows<WorkflowBookmarkRecord, WorkflowInstanceRecord> _tenant;

    /// <summary>
    /// 创建（作用域服务）
    /// </summary>
    public DatabaseWorkflowBookmarkStore(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var sequence = services.GetRequiredService<WriteSequence>();
        _platform = new BookmarkRows<PlatformWorkflowBookmarkRecord, PlatformWorkflowInstanceRecord>(services.GetRequiredService<IRepository<PlatformWorkflowBookmarkRecord, string>>(), services.GetRequiredService<IRepository<PlatformWorkflowInstanceRecord, string>>(), sequence);
        _tenant = new BookmarkRows<WorkflowBookmarkRecord, WorkflowInstanceRecord>(services.GetRequiredService<IRepository<WorkflowBookmarkRecord, string>>(), services.GetRequiredService<IRepository<WorkflowInstanceRecord, string>>(), sequence);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowBookmark>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindAsync(id, cancellationToken).ConfigureAwait(false) : await _tenant.FindAsync(id, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.FindManyAsync(ids, cancellationToken).ConfigureAwait(false) : await _tenant.FindManyAsync(ids, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByInstanceAsync(string? tenantId, string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.ByInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false) : await _tenant.ByInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> DueAsync(string? tenantId, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.DueAsync(now, limit, cancellationToken).ConfigureAwait(false) : await _tenant.DueAsync(now, limit, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByKindAndKeyAsync(string? tenantId, string kind, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(key);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.ByKindAndKeyAsync(kind, key, cancellationToken).ConfigureAwait(false) : await _tenant.ByKindAndKeyAsync(kind, key, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ListUserTasksAsync(string? tenantId, UserTaskQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.ListUserTasksAsync(query, cancellationToken).ConfigureAwait(false) : await _tenant.ListUserTasksAsync(query, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<long>> CountUserTasksAsync(string? tenantId, UserTaskQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.CountUserTasksAsync(query, cancellationToken).ConfigureAwait(false) : await _tenant.CountUserTasksAsync(query, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> AvailableAsync(string? tenantId, string kind, string key, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(key);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.AvailableAsync(kind, key, now, limit, cancellationToken).ConfigureAwait(false) : await _tenant.AvailableAsync(kind, key, now, limit, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> BySignalAsync(string? tenantId, string signalName, string? correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signalName);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.BySignalAsync(signalName, correlationId, cancellationToken).ConfigureAwait(false) : await _tenant.BySignalAsync(signalName, correlationId, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        Enter(tenantId);
        foreach (var bookmark in bookmarks)
        {
            StorePartition.EnsureOwned(tenantId, bookmark.TenantId, "bookmark", bookmark.Id);
        }

        return StorePartition.OrUnavailable(tenantId is null ? await _platform.SaveAsync(bookmarks, cancellationToken).ConfigureAwait(false) : await _tenant.SaveAsync(bookmarks, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.DeleteAsync(ids, cancellationToken).ConfigureAwait(false) : await _tenant.DeleteAsync(ids, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteByInstanceAsync(string? tenantId, IReadOnlyList<string> instanceIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);
        Enter(tenantId);
        return StorePartition.OrUnavailable(tenantId is null ? await _platform.DeleteByInstanceAsync(instanceIds, cancellationToken).ConfigureAwait(false) : await _tenant.DeleteByInstanceAsync(instanceIds, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 核对分区与工作单元的租户一致
    /// </summary>
    private void Enter(string? tenantId) => StorePartition.Ensure(_unitOfWork, tenantId);
}
