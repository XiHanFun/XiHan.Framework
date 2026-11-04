// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Storage;

/// <summary>
/// 定义存储
/// </summary>
/// <remarks>
/// 按分区读写：每个方法的第一个参数 <c>tenantId</c> 是分区（空为平台，否则是该租户），只返回这个分区的定义，写入的定义 <see cref="WorkflowDefinition.TenantId"/> 必须等于它；名字与版本号在分区里唯一。写入按批量。不可达 → <see cref="WorkflowErrors.StoreUnavailable"/>，容量已满 → <see cref="WorkflowErrors.StoreCapacity"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowDefinitionStore
{
    /// <summary>
    /// 按 ID 找
    /// </summary>
    /// <remarks>
    /// 别的分区的定义不返回
    /// </remarks>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 批量找
    /// </summary>
    /// <remarks>
    /// 不存在或在别的分区的不在结果里，顺序不保证
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 按名字与版本找
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string? tenantId, WorkflowName name, int version, CancellationToken cancellationToken);

    /// <summary>
    /// 该名字下版本号最大的已发布定义
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(string? tenantId, WorkflowName name, CancellationToken cancellationToken);

    /// <summary>
    /// 该名字下的最大版本号
    /// </summary>
    /// <remarks>
    /// 没有为 0
    /// </remarks>
    ValueTask<Result<int>> MaxVersionAsync(string? tenantId, WorkflowName name, CancellationToken cancellationToken);

    /// <summary>
    /// 整份定义的列表：名字升序、版本降序
    /// </summary>
    /// <remarks>
    /// 不分页，引擎的信号启动用它找监听该信号的已发布定义；面向列表页的查询用 <see cref="ListSummariesAsync"/>
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(string? tenantId, WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// 按定义查询列出摘要的一页
    /// </summary>
    /// <remarks>
    /// 过滤项见 <see cref="WorkflowDefinitionQuery.Matches"/>（关键字按 <see cref="WorkflowKeyword"/> 折叠后匹配名字、显示名或说明，分类精确匹配，创建时间下界含、上界不含）；按排序项 <see cref="WorkflowDefinitionQuery.Sorts"/> 依次排序（空时名字升序），再接次序键名字升序、版本降序（排序项里已有的字段跳过，ADR-0104），标题按 <see cref="WorkflowSortKeys.Title"/> 比较（ADR-0102），状态按生命周期的先后（ADR-0103）；跳过 <see cref="WorkflowDefinitionQuery.Skip"/> 条后最多 <see cref="WorkflowDefinitionQuery.Limit"/> 条。分页与排序项已由调用方校验：排序项为空抛出 <see cref="ArgumentException"/>，字段不在白名单里抛出 <see cref="ArgumentOutOfRangeException"/>（重复的字段只按第一次出现的排）
    /// </remarks>
    ValueTask<Result<IReadOnlyList<WorkflowDefinitionSummary>>> ListSummariesAsync(string? tenantId, WorkflowDefinitionQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 与 <see cref="ListSummariesAsync"/> 同条件的定义总数
    /// </summary>
    /// <remarks>
    /// 不看分页
    /// </remarks>
    ValueTask<Result<long>> CountAsync(string? tenantId, WorkflowDefinitionQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 新增或整体替换
    /// </summary>
    ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken);

    /// <summary>
    /// 删除
    /// </summary>
    /// <remarks>
    /// 不存在或在别的分区的忽略
    /// </remarks>
    ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
