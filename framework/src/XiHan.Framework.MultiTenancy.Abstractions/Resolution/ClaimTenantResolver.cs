// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Resolution;

/// <summary>
/// 从已认证主体的声明解析租户（声明类型见 <see cref="MultiTenancyOptions.TenantClaim"/>）
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ClaimTenantResolver(MultiTenancyOptions options) : ITenantResolver
{
    /// <summary>
    /// 提供承载租户 ID 的声明类型
    /// </summary>
    private readonly MultiTenancyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public ValueTask<Result<Maybe<string>>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 未认证主体的声明不可信，直接放弃，交给后续解析器（如 Baggage）
        var principal = context.GetSecurity().Principal;
        if (!principal.IsAuthenticated)
        {
            return ValueTask.FromResult(Result.Success(Maybe.None<string>()));
        }

        // 同类声明可能有多个：值都相同按一个算；值不同是签发方给出了矛盾的身份，取哪个都可能是错的租户，拒绝而不是取第一个（ADR-0218）
        string? tenant = null;
        foreach (var value in principal.ClaimValues(_options.TenantClaim))
        {
            if (tenant is null)
            {
                tenant = value;
            }
            else if (!string.Equals(tenant, value, StringComparison.Ordinal))
            {
                return ValueTask.FromResult<Result<Maybe<string>>>(Error.Validation(MultiTenancyErrors.ClaimsConflict, MultiTenancyMessages.ClaimsConflict));
            }
        }

        return ValueTask.FromResult(Result.Success(tenant is null ? Maybe.None<string>() : Maybe.Some(tenant)));
    }
}
