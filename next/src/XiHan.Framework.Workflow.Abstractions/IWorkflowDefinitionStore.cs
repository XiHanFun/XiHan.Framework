// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 定义存储；不按租户过滤，写入按批量。不可达 → <see cref="WorkflowErrors.StoreUnavailable"/>，容量已满 → <see cref="WorkflowErrors.StoreCapacity"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowDefinitionStore
{
    /// <summary>
    /// 按 ID 找。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 批量找；不存在的不在结果里，顺序不保证。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 按名字与版本找。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(WorkflowName name, int version, CancellationToken cancellationToken);

    /// <summary>
    /// 该名字下版本号最大的已发布定义。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(WorkflowName name, CancellationToken cancellationToken);

    /// <summary>
    /// 该名字下的最大版本号；没有为 0。
    /// </summary>
    ValueTask<Result<int>> MaxVersionAsync(WorkflowName name, CancellationToken cancellationToken);

    /// <summary>
    /// 列表：名字升序、版本降序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换。
    /// </summary>
    ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken);

    /// <summary>
    /// 删除；不存在的忽略。
    /// </summary>
    ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
