// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 策略：主体已认证。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class AuthenticatedPolicy : IPolicy
{
    /// <summary>
    /// 策略名 <c>authenticated</c>。
    /// </summary>
    public const string PolicyName = "authenticated";

    /// <inheritdoc />
    public string Name => PolicyName;

    /// <inheritdoc />
    public ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(request.Security.Principal.IsAuthenticated
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny(Error.Denied(SecurityErrors.NotAuthenticated, SecurityMessages.AuthenticationRequired)));
    }
}
