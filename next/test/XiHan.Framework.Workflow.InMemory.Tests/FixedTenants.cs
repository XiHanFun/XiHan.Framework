// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 固定几个启用租户的租户目录。
/// </summary>
/// <remarks>
/// 轮询器从它取分区：平台之外每个租户一个分区。
/// </remarks>
internal sealed class FixedTenants(params string[] ids) : ITenantStore
{
    /// <summary>
    /// 全部租户。
    /// </summary>
    private readonly IReadOnlyList<TenantInfo> _tenants = [.. ids.Select(static id => new TenantInfo { Id = id })];

    /// <inheritdoc />
    public ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken)
        => ValueTask.FromResult(_tenants.FirstOrDefault(tenant => string.Equals(tenant.Id, tenantId, StringComparison.Ordinal)) is { } found ? Maybe.Some(found) : Maybe.None<TenantInfo>());

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_tenants);
}
