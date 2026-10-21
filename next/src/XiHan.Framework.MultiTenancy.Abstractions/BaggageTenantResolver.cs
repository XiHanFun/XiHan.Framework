// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 从 Baggage 的 <see cref="BaggageKeys.Tenant"/> 解析租户：客户端或上游服务自报的租户。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class BaggageTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    public ValueTask<Maybe<string>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.TryGetBaggage(BaggageKeys.Tenant, out var tenantId) ? Maybe.Some(tenantId) : default);
    }
}
