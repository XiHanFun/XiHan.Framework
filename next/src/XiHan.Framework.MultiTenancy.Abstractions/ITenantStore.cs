// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 租户存储，Provider 实现它。ID 区分大小写。基础设施失败抛出异常，不伪装成不存在。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ITenantStore
{
    /// <summary>
    /// 按 ID 查找。
    /// </summary>
    ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 枚举全部租户。
    /// </summary>
    ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 从调用上下文解析租户 ID 候选。按注册顺序尝试，先解析出的生效；只解析，不校验。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ITenantResolver
{
    /// <summary>
    /// 解析；无法解析时无值。
    /// </summary>
    ValueTask<Maybe<string>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// 从已认证主体的声明解析租户（声明类型见 <see cref="MultiTenancyOptions.TenantClaim"/>）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ClaimTenantResolver(MultiTenancyOptions options) : ITenantResolver
{
    private readonly MultiTenancyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public ValueTask<Maybe<string>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var principal = context.GetSecurity().Principal;
        if (!principal.IsAuthenticated)
        {
            return ValueTask.FromResult<Maybe<string>>(default);
        }

        foreach (var value in principal.ClaimValues(_options.TenantClaim))
        {
            return ValueTask.FromResult(Maybe.Some(value));
        }

        return ValueTask.FromResult<Maybe<string>>(default);
    }
}

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
