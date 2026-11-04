// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义按名字的解析，目录与引擎共用
/// </summary>
/// <remarks>
/// 只在调用方的分区里解析（平台或该租户）：没有全局定义，也不退回到别的分区（ADR-0077 推翻了 ADR-0066 第 5 节的全局定义）
/// </remarks>
internal static class WorkflowDefinitionNamespace
{
    /// <summary>
    /// 在分区里按名字（可带版本）找已发布定义
    /// </summary>
    /// <remarks>
    /// 没给版本取最新已发布的；给了版本只认已发布的那一版
    /// </remarks>
    public static async ValueTask<Result<Maybe<WorkflowDefinition>>> FindPublishedAsync(IWorkflowDefinitionStore store, string? tenantId, WorkflowName name, int? version, CancellationToken cancellationToken)
    {
        if (version is null)
        {
            return await store.FindLatestPublishedAsync(tenantId, name, cancellationToken).ConfigureAwait(false);
        }

        var found = await store.FindAsync(tenantId, name, version.Value, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        return maybe.TryGetValue(out var definition) && definition.Status == WorkflowDefinitionStatus.Published ? Maybe.Some(definition) : Maybe.None<WorkflowDefinition>();
    }
}
