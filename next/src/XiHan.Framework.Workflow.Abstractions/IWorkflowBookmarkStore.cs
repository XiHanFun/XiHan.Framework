// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 书签存储；不按租户过滤，写入按批量。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowBookmarkStore
{
    /// <summary>
    /// 按 ID 找。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowBookmark>>> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 批量找；不存在的不在结果里。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 实例的全部书签，创建时间升序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByInstanceAsync(string instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// 到期的书签：<c>DueAt</c> 不为空且不晚于 <paramref name="now"/>，到期时间升序，最多 <paramref name="limit"/> 条。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> DueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 按种类与键找，创建时间升序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken);

    /// <summary>
    /// 信号书签：种类 <c>Signal</c>、键为信号名；给了关联 ID 时取关联为空或相等的，否则全部。创建时间升序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> BySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换。
    /// </summary>
    ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 删除；不存在的忽略。
    /// </summary>
    ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 删除这些实例的全部书签。
    /// </summary>
    ValueTask<Result> DeleteByInstanceAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken);
}
