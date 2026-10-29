// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Authentication;

/// <summary>
/// ASP.NET Core 认证方案 <c>XiHan</c> 的常量。
/// </summary>
/// <remarks>
/// 给绕过内核管道的端点（SignalR Hub、Server-Sent Events 等）用，认证交给内核已装配的认证处理器（RFC-0008，ADR-0051）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class InvocationAuthenticationDefaults
{
    /// <summary>
    /// 方案名。
    /// </summary>
    public const string AuthenticationScheme = "XiHan";

    /// <summary>
    /// 查询串令牌的参数名：只在登记的路径前缀下接受（<see cref="InvocationAuthenticationOptions.QueryTokenPaths"/>）。
    /// </summary>
    public const string QueryTokenParameter = "access_token";

    /// <summary>
    /// 查询串令牌作为凭据时的方案。
    /// </summary>
    public const string QueryTokenScheme = "Bearer";
}
