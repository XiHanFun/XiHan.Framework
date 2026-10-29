// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义目录。
/// </summary>
/// <remarks>
/// 定义的生命周期（草稿 → 发布 → 停用 / 归档）与版本。按标识的操作都是批量、逐项结果；状态不允许 → <see cref="WorkflowErrors.DefinitionStatus"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowDefinitionCatalog
{
    /// <summary>
    /// 新建草稿。
    /// </summary>
    /// <remarks>
    /// 分配新 ID，版本号 = 同名最大版本 + 1，状态 Draft，租户缺省取当前调用的租户；返回存下的定义。
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
    ValueTask<Result<Maybe<WorkflowDefinition>>> FindPublishedAsync(WorkflowName name, int? version, CancellationToken cancellationToken);

    /// <summary>
    /// 列表：名字升序、版本降序。
    /// </summary>
    ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// 已登记活动的描述符（设计器用），按类型名排序。
    /// </summary>
    IReadOnlyList<Activities.ActivityDescriptor> ActivityTypes();
}
