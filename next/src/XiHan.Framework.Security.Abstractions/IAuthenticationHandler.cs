// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 把某一方案的凭据变成主体。基础设施失败抛异常；凭据无效以 <see cref="SecurityErrors.InvalidCredential"/> 返回。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IAuthenticationHandler
{
    /// <summary>
    /// 处理的方案。
    /// </summary>
    string Scheme { get; }

    /// <summary>
    /// 认证。
    /// </summary>
    ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken);
}
