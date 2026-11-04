// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义目录
/// </summary>
/// <remarks>
/// 定义的生命周期（草稿 → 发布 → 停用 / 归档）与版本。按标识的操作都是批量、逐项结果；状态不允许 → <see cref="WorkflowErrors.DefinitionStatus"/>。定义按分区严格隔离（ADR-0077）：平台一个分区、每个租户一个分区，调用只看得到、改得动自己分区的定义——平台调用只有平台的，租户只有本租户的，别的分区的一律按不存在处理（<see cref="WorkflowErrors.DefinitionNotFound"/>）；名字与版本号在分区里编号，没有全局定义，平台建的定义租户看不到
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowDefinitionCatalog
{
    /// <summary>
    /// 新建草稿
    /// </summary>
    /// <remarks>
    /// 分配新 ID，状态 Draft，定义属于调用方的分区（<see cref="WorkflowDefinition.TenantId"/> 取调用的租户，平台为空；写了别的租户 → <see cref="WorkflowErrors.DefinitionNotFound"/>，不替调用方改写），版本号 = 该分区里同名最大版本 + 1；返回存下的定义
    /// </remarks>
    ValueTask<Result<WorkflowDefinition>> CreateAsync(WorkflowDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// 更新草稿（只允许 Draft）：名字、版本、创建时间沿用存下的
    /// </summary>
    ValueTask<Result<WorkflowDefinition>> UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// 发布（只允许 Draft，先校验）
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> PublishAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 以该名字的最新版本为底复制一个新草稿
    /// </summary>
    /// <remarks>
    /// 只在调用方的分区里找，新草稿留在同一分区；没有 → <see cref="WorkflowErrors.DefinitionNotFound"/>
    /// </remarks>
    ValueTask<Result<WorkflowDefinition>> NewVersionAsync(WorkflowName name, CancellationToken cancellationToken);

    /// <summary>
    /// 停用（只允许 Published）
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> DisableAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 归档（任何状态）
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> ArchiveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 删除（只允许 Draft）
    /// </summary>
    ValueTask<IReadOnlyList<Result>> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 找
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// 找已发布版本：给了版本号就找该版本（须 Published），否则最新已发布版本
    /// </summary>
    /// <remarks>
    /// 只在调用方的分区里找，找不到不退回别的分区（没有全局定义）
    /// </remarks>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindPublishedAsync(WorkflowName name, int? version, CancellationToken cancellationToken);

    /// <summary>
    /// 按条件列定义摘要的一页，带同条件的总数
    /// </summary>
    /// <remarks>
    /// 只列调用方分区的定义；按关键字（名字、显示名、说明）、名字、分类、状态与创建时间区间过滤，按排序项 <see cref="WorkflowDefinitionQuery.Sorts"/> 排序（多字段，缺省名字升序、版本降序）。过滤、排序、分页与计数都由存储执行（ADR-0096、ADR-0102、ADR-0104）。<see cref="WorkflowDefinitionQuery.Skip"/> 为负或 <see cref="WorkflowDefinitionQuery.Limit"/> 不在 1 到 <see cref="WorkflowDefinitionQuery.MaxLimit"/> 之间 → <c>WORKFLOW_VALIDATION_002</c>（HTTP 400，消息键 <c>workflow.definition.query_page_invalid</c>）；排序项不合法 → 同一错误码：项数超过 <see cref="WorkflowDefinitionQuery.MaxSorts"/>（消息键 <c>workflow.definition.query_sorts_too_many</c>），某项为空或字段不是 <see cref="WorkflowDefinitionSortField"/> 里定义的值（<c>workflow.definition.query_sort_invalid</c>），同一字段出现两次（<c>workflow.definition.query_sort_duplicate</c>）；都不进存储
    /// </remarks>
    ValueTask<Result<WorkflowDefinitionPage>> QueryAsync(WorkflowDefinitionQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 已登记活动的描述符（设计器用），按类型名排序
    /// </summary>
    /// <remarks>
    /// 显示名、分类显示名、说明与输入说明按当前消息作用域渲染（<see cref="Activities.ActivityDescriptor.Localize"/>，调用链里是调用方的区域，ADR-0106）
    /// </remarks>
    IReadOnlyList<Activities.ActivityDescriptor> ActivityTypes();
}
