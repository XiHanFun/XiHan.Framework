// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Security.Abstractions.Tests;

/// <summary>
/// 固定方案名、把认证委托给回调的处理器。
/// </summary>
internal sealed class StaticHandler(string scheme, Func<AuthenticationCredential, Result<Principal>> authenticate) : IAuthenticationHandler
{
    /// <inheritdoc />
    public string Scheme => scheme;

    /// <inheritdoc />
    public ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(authenticate(credential));
}
