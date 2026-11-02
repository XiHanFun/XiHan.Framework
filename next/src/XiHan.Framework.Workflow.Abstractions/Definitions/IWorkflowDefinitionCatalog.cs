// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义目录。
/// </summary>
/// <remarks>
/// 定义的生命周期（草稿 → 发布 → 停用 / 归档）与版本。按标识的操作都是批量、逐项结果；状态不允许 → <see cref="WorkflowErrors.DefinitionStatus"/>。定义按租户隔离：名字与版本号在租户（全局定义为空）的名字空间里编号；有租户的调用看得到本租户与全局定义、只改得动本租户的，别的按不存在处理；无租户的调用看得到、改得动全部。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowDefinitionCatalog
{
    /// <summary>
    /// 新建草稿。
    /// </summary>
    /// <remarks>
    /// 分配新 ID，状态 Draft，租户缺省取当前调用的租户，版本号 = 该租户名字空间里同名最大版本 + 1；返回存下的定义。
    /// </remarks>
    ValueTask<Result<WorkflowDefinition>> CreateAsync(WorkflowDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// 更新草稿（只允许 Draft）：名字、版本、创建时间沿用存下的。
    /// </summary>
    ValueTask<Result<WorkflowDefinition>> UpdateAsync(WorkflowDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// 发布（只允许 Draft，先校验）。
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> PublishAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 以该名字的最新版本为底复制一个新草稿。
    /// </summary>
    /// <remarks>
    /// 只在当前调用租户的名字空间里找，新草稿属于同一名字空间；没有 → <see cref="WorkflowErrors.DefinitionNotFound"/>。
    /// </remarks>
    ValueTask<Result<WorkflowDefinition>> NewVersionAsync(WorkflowName name, CancellationToken cancellationToken);

    /// <summary>
    /// 停用（只允许 Published）。
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> DisableAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 归档（任何状态）。
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowDefinition>>> ArchiveAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 删除（只允许 Draft）。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>
    /// 按 ID 找。
    /// </summary>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// 找已发布版本：给了版本号就找该版本（须 Published），否则最新已发布版本。
    /// </summary>
    /// <remarks>
    /// 先找当前调用租户的名字空间，没有再找全局定义。
    /// </remarks>
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindPublishedAsync(WorkflowName name, int? version, CancellationToken cancellationToken);

    /// <summary>
    /// 按条件列定义摘要的一页，带同条件的总数。
    /// </summary>
    /// <remarks>
    /// 只列调用方分区的定义；按关键字（名字、显示名、说明）、名字、分类与状态过滤，名字升序、版本降序。过滤、排序、分页与计数都由存储执行（ADR-0096）。<see cref="WorkflowDefinitionQuery.Skip"/> 为负或 <see cref="WorkflowDefinitionQuery.Limit"/> 不在 1 到 <see cref="WorkflowDefinitionQuery.MaxLimit"/> 之间 → <c>WORKFLOW_VALIDATION_002</c>（HTTP 400，消息键 <c>workflow.definition.query_page_invalid</c>），不进存储。
    /// </remarks>
    ValueTask<Result<WorkflowDefinitionPage>> QueryAsync(WorkflowDefinitionQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 已登记活动的描述符（设计器用），按类型名排序。
    /// </summary>
    IReadOnlyList<Activities.ActivityDescriptor> ActivityTypes();
}
