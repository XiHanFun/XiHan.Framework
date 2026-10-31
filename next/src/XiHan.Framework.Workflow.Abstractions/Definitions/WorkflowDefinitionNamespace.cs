// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义按租户划分的名字空间与可见性。
/// </summary>
/// <remarks>
/// 定义属于一个租户，或是全局定义（租户为空）；名字与版本号在各自的名字空间里编号。有租户的调用看得到本租户与全局定义、只改得动本租户的；无租户的调用看得到、改得动全部。目录与引擎共用这套规则（RFC-0021、ADR-0066）。
/// </remarks>
internal static class WorkflowDefinitionNamespace
{
    /// <summary>
    /// 调用方看得到这个定义。
    /// </summary>
    public static bool IsVisible(WorkflowDefinition definition, string? tenantId)
        => tenantId is null || definition.TenantId is null || string.Equals(definition.TenantId, tenantId, StringComparison.Ordinal);

    /// <summary>
    /// 调用方改得动这个定义。
    /// </summary>
    public static bool IsWritable(WorkflowDefinition definition, string? tenantId)
        => tenantId is null || string.Equals(definition.TenantId, tenantId, StringComparison.Ordinal);

    /// <summary>
    /// 按名字找已发布定义：先找租户的名字空间，没有再找全局定义。
    /// </summary>
    /// <remarks>
    /// 给了版本号找该版本（须已发布），否则找最新已发布版本。租户为空只找全局定义。本租户有已发布的同名定义时全局定义不参与。
    /// </remarks>
    public static async ValueTask<Result<Maybe<WorkflowDefinition>>> FindPublishedAsync(IWorkflowDefinitionStore store, WorkflowName name, int? version, string? tenantId, CancellationToken cancellationToken)
    {
        var own = await FindPublishedInAsync(store, name, version, tenantId, cancellationToken).ConfigureAwait(false);
        if (tenantId is null || !own.TryGetValue(out var found) || found.HasValue)
        {
            return own;
        }

        return await FindPublishedInAsync(store, name, version, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 在一个名字空间里找已发布定义。
    /// </summary>
    private static async ValueTask<Result<Maybe<WorkflowDefinition>>> FindPublishedInAsync(IWorkflowDefinitionStore store, WorkflowName name, int? version, string? tenantId, CancellationToken cancellationToken)
    {
        if (version is null)
        {
            return await store.FindLatestPublishedAsync(name, tenantId, cancellationToken).ConfigureAwait(false);
        }

        var found = await store.FindAsync(name, version.Value, tenantId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        return maybe.TryGetValue(out var definition) && definition.Status == WorkflowDefinitionStatus.Published ? Maybe.Some(definition) : Maybe.None<WorkflowDefinition>();
    }
}
