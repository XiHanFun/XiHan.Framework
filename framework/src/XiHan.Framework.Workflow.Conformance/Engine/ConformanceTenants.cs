// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 套件的租户目录：<c>t1</c>、<c>t2</c>、<c>t-other</c> 启用，<c>t-off</c> 停用
/// </summary>
/// <remarks>
/// 轮询器从租户目录取要覆盖的租户分区；宿主装配了多租户时用它自己的目录（套件以 <c>TryAdd</c> 登记，让位于宿主的）
/// </remarks>
internal sealed class ConformanceTenants : ITenantStore
{
    /// <summary>
    /// 停用的租户
    /// </summary>
    public const string Disabled = "t-off";

    /// <summary>
    /// 全部租户
    /// </summary>
    private static readonly IReadOnlyList<TenantInfo> _tenants =
    [
        new TenantInfo { Id = "t1" },
        new TenantInfo { Id = "t2" },
        new TenantInfo { Id = "t-other" },
        new TenantInfo { Id = Disabled, IsEnabled = false },
    ];

    /// <inheritdoc />
    public ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(_tenants.FirstOrDefault(tenant => string.Equals(tenant.Id, tenantId, StringComparison.Ordinal)) is { } found ? Maybe.Some(found) : Maybe.None<TenantInfo>()));

    /// <inheritdoc />
    public ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(new TenantPage { Items = [.. _tenants.Where(tenant => !query.EnabledOnly || tenant.IsEnabled)] }));
}
