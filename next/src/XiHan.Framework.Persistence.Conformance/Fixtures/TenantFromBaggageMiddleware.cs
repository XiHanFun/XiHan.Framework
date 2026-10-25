// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 从 Baggage 的 <see cref="BaggageKeys.Tenant"/> 解析租户。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class TenantFromBaggageMiddleware : IPipelineMiddleware
{
    /// <inheritdoc />
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);
        // 没有租户 Baggage 时不写安全上下文，调用以"无租户"进入 Transaction 阶段，用例据此验证租户缺失的行为
        if (context.TryGetBaggage(BaggageKeys.Tenant, out var tenantId))
        {
            context.SetTenant(new TenantContext(tenantId));
        }

        return nextStep(context);
    }
}
