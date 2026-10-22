// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 从已认证主体的声明解析租户（声明类型见 <see cref="MultiTenancyOptions.TenantClaim"/>）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ClaimTenantResolver(MultiTenancyOptions options) : ITenantResolver
{
    /// <summary>
    /// 提供承载租户 ID 的声明类型。
    /// </summary>
    private readonly MultiTenancyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public ValueTask<Maybe<string>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 未认证主体的声明不可信，直接放弃，交给后续解析器（如 Baggage）
        var principal = context.GetSecurity().Principal;
        if (!principal.IsAuthenticated)
        {
            return ValueTask.FromResult<Maybe<string>>(default);
        }

        // 同类声明可能有多个，只取第一个
        foreach (var value in principal.ClaimValues(_options.TenantClaim))
        {
            return ValueTask.FromResult(Maybe.Some(value));
        }

        return ValueTask.FromResult<Maybe<string>>(default);
    }
}
