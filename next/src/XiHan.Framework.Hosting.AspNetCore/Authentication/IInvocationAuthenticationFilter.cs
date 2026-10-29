// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Authentication;

/// <summary>
/// 认证方案 <c>XiHan</c> 的挂点。
/// </summary>
/// <remarks>
/// 内核的认证处理器认证成功后依次调用（按登记顺序），任一返回失败即认证失败（401）。用于应用自己的核对，如令牌所属的会话仍然有效；框架不判定会话。按作用域或单例登记为本接口即可。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IInvocationAuthenticationFilter
{
    /// <summary>
    /// 核对已认证的主体。
    /// </summary>
    /// <remarks>
    /// 上下文是以本次请求建的调用上下文，其安全上下文已写入该主体。
    /// </remarks>
    ValueTask<Result> AuthenticatedAsync(Principal principal, InvocationContext context, CancellationToken cancellationToken);
}
