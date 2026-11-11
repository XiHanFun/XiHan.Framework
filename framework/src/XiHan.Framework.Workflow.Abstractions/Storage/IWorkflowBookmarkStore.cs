// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;

namespace XiHan.Framework.Workflow.Abstractions.Storage;

/// <summary>
/// 书签存储
/// </summary>
/// <remarks>
/// 按分区读写：每个方法的第一个参数 <c>tenantId</c> 是分区（空为平台，否则是该租户），只返回这个分区的书签，写入的书签 <see cref="WorkflowBookmark.TenantId"/> 必须等于它。写入按批量
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowBookmarkStore
{
    /// <summary>
    /// 按 ID 找
    /// </summary>
    ValueTask<Result<Maybe<WorkflowBookmark>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 批量找
    /// </summary>
    /// <remarks>
    /// 不存在或在别的分区的不在结果里
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 实例的全部书签，创建时间升序
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByInstanceAsync(string? tenantId, string instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// 到期的书签：<c>DueAt</c> 不为空且不晚于 <paramref name="now"/>，到期时间升序，最多 <paramref name="limit"/> 条
    /// </summary>
    /// <remarks>
    /// 轮询器逐个分区调用它（平台与每个启用的租户）
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> DueAsync(string? tenantId, DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 按种类与键找，创建时间升序
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByKindAndKeyAsync(string? tenantId, string kind, string key, CancellationToken cancellationToken);

    /// <summary>
    /// 按待办查询的条件列出用户任务书签的一页
    /// </summary>
    /// <remarks>
    /// 只含种类为 <see cref="BookmarkKinds.UserTask"/>、所属实例在运行中（<see cref="WorkflowInstanceStatus.Running"/>）的书签；<see cref="UserTaskQuery.AssigneeId"/> 匹配书签的键，其余条件匹配书签的实例、节点、所属实例的定义名与创建时间；<see cref="UserTaskQuery.Keyword"/> 按 <see cref="WorkflowKeyword"/> 折叠后匹配书签载荷的 <c>title</c>、所属实例的标题、定义名或关联 ID（ADR-0096、ADR-0099）。按创建时间排序（<see cref="UserTaskQuery.Descending"/>），同刻按写入先后，跳过 <see cref="UserTaskQuery.Skip"/> 条后最多 <see cref="UserTaskQuery.Take"/> 条。分页已由调用方校验。实例的状态变了（挂起、恢复、故障、重试），它的书签随之进出结果
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ListUserTasksAsync(string? tenantId, UserTaskQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 与 <see cref="ListUserTasksAsync"/> 同条件的用户任务书签总数
    /// </summary>
    /// <remarks>
    /// 不看分页与排序
    /// </remarks>
    ValueTask<Result<long>> CountUserTasksAsync(string? tenantId, UserTaskQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 种类与键匹配、此刻可以领取的书签，创建时间升序，最多 <paramref name="limit"/> 条
    /// </summary>
    /// <remarks>
    /// 可以领取：<c>DueAt</c> 为空或不晚于 <paramref name="now"/>。外部任务的领取用它把可领取时刻与条数一起下推到存储查询，不读回该主题的全部书签
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> AvailableAsync(string? tenantId, string kind, string key, DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 信号书签
    /// </summary>
    /// <remarks>
    /// 种类 <c>Signal</c>、键为信号名；给了关联 ID 时取关联为空或相等的，否则全部。创建时间升序
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> BySignalAsync(string? tenantId, string signalName, string? correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换
    /// </summary>
    ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 删除
    /// </summary>
    /// <remarks>
    /// 不存在或在别的分区的忽略
    /// </remarks>
    ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 删除这些实例的全部书签
    /// </summary>
    ValueTask<Result> DeleteByInstanceAsync(string? tenantId, IReadOnlyList<string> instanceIds, CancellationToken cancellationToken);
}
