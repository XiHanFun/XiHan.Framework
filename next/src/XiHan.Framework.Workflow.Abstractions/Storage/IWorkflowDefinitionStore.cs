// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Storage;

/// <summary>
/// 定义存储。
/// </summary>
/// <remarks>
/// 不按租户过滤，写入按批量。按名字的查询以租户为名字空间：<c>tenantId</c> 按相等比较，<see langword="null"/> 只匹配全局定义（无租户）。不可达 → <see cref="WorkflowErrors.StoreUnavailable"/>，容量已满 → <see cref="WorkflowErrors.StoreCapacity"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowDefinitionStore
{
    /// <summary>
    /// 按 ID 找。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 批量找。
    /// </summary>
    /// <remarks>
    /// 不存在的不在结果里，顺序不保证。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 在一个名字空间里按名字与版本找。
    /// </summary>
    /// <remarks>
    /// 名字空间是 <paramref name="tenantId"/>（空为全局定义）。
    /// </remarks>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(WorkflowName name, int version, string? tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 一个名字空间里该名字下版本号最大的已发布定义。
    /// </summary>
    /// <remarks>
    /// 名字空间是 <paramref name="tenantId"/>（空为全局定义）。
    /// </remarks>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(WorkflowName name, string? tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 一个名字空间里该名字下的最大版本号。
    /// </summary>
    /// <remarks>
    /// 名字空间是 <paramref name="tenantId"/>（空为全局定义）；没有为 0。
    /// </remarks>
    ValueTask<Result<int>> MaxVersionAsync(WorkflowName name, string? tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 列表：名字升序、版本降序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换。
    /// </summary>
    ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken);

    /// <summary>
    /// 删除。
    /// </summary>
    /// <remarks>
    /// 不存在的忽略。
    /// </remarks>
    ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
