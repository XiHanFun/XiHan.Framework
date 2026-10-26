// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 以 Bearer 方案工作的认证处理器桩：只用来让 OpenAPI 文档发布 Bearer 方案，不认证任何凭据。
/// </summary>
internal sealed class BearerHandler : IAuthenticationHandler
{
    /// <inheritdoc />
    public string Scheme => "Bearer";

    /// <inheritdoc />
    public ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<Principal>>(Error.Literal("SECURITY_DENIED_003", "The stub handler does not authenticate."));
}
