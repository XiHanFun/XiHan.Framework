// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 单个定义的生命周期操作：批量方法的特例。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowDefinitionCatalogExtensions
{
    /// <summary>
    /// 发布一个定义。
    /// </summary>
    public static async ValueTask<Result<WorkflowDefinition>> PublishAsync(this IWorkflowDefinitionCatalog catalog, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return (await catalog.PublishAsync([id], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 停用一个定义。
    /// </summary>
    public static async ValueTask<Result<WorkflowDefinition>> DisableAsync(this IWorkflowDefinitionCatalog catalog, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return (await catalog.DisableAsync([id], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 归档一个定义。
    /// </summary>
    public static async ValueTask<Result<WorkflowDefinition>> ArchiveAsync(this IWorkflowDefinitionCatalog catalog, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return (await catalog.ArchiveAsync([id], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 删除一个定义。
    /// </summary>
    public static async ValueTask<Result> DeleteAsync(this IWorkflowDefinitionCatalog catalog, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return (await catalog.DeleteAsync([id], cancellationToken).ConfigureAwait(false))[0];
    }
}
