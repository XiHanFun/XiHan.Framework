// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Storage;

/// <summary>
/// 定义存储。
/// </summary>
/// <remarks>
/// 按分区读写：每个方法的第一个参数 <c>tenantId</c> 是分区（空为平台，否则是该租户），只返回这个分区的定义，写入的定义 <see cref="WorkflowDefinition.TenantId"/> 必须等于它；名字与版本号在分区里唯一。写入按批量。不可达 → <see cref="WorkflowErrors.StoreUnavailable"/>，容量已满 → <see cref="WorkflowErrors.StoreCapacity"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowDefinitionStore
{
    /// <summary>
    /// 按 ID 找。
    /// </summary>
    /// <remarks>
    /// 别的分区的定义不返回。
    /// </remarks>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 批量找。
    /// </summary>
    /// <remarks>
    /// 不存在或在别的分区的不在结果里，顺序不保证。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 按名字与版本找。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string? tenantId, WorkflowName name, int version, CancellationToken cancellationToken);

    /// <summary>
    /// 该名字下版本号最大的已发布定义。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(string? tenantId, WorkflowName name, CancellationToken cancellationToken);

    /// <summary>
    /// 该名字下的最大版本号。
    /// </summary>
    /// <remarks>
    /// 没有为 0。
    /// </remarks>
    ValueTask<Result<int>> MaxVersionAsync(string? tenantId, WorkflowName name, CancellationToken cancellationToken);

    /// <summary>
    /// 列表：名字升序、版本降序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(string? tenantId, WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换。
    /// </summary>
    ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken);

    /// <summary>
    /// 删除。
    /// </summary>
    /// <remarks>
    /// 不存在或在别的分区的忽略。
    /// </remarks>
    ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
