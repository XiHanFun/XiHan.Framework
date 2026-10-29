// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Authentication;

/// <summary>
/// 登记认证方案 <c>XiHan</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class InvocationAuthenticationBuilderExtensions
{
    /// <summary>
    /// 登记认证方案 <see cref="InvocationAuthenticationDefaults.AuthenticationScheme"/>（不设为缺省方案：端点经 <c>RequireAuthorization</c> 的策略或 <c>[Authorize(AuthenticationSchemes = "XiHan")]</c> 选用它）。
    /// 可多次调用：方案只登记一次，各次的 <paramref name="configure"/> 依次生效（如各模块各加自己的查询串令牌路径）。宿主须在映射这些端点前 <c>UseAuthentication()</c> / <c>UseAuthorization()</c>。
    /// </summary>
    public static AuthenticationBuilder AddXiHan(this AuthenticationBuilder builder, Action<InvocationAuthenticationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!builder.Services.Any(static d => d.ServiceType == typeof(InvocationAuthenticationHandler)))
        {
            builder.AddScheme<InvocationAuthenticationOptions, InvocationAuthenticationHandler>(InvocationAuthenticationDefaults.AuthenticationScheme, displayName: null, configureOptions: null);
        }

        if (configure is not null)
        {
            builder.Services.Configure(InvocationAuthenticationDefaults.AuthenticationScheme, configure);
        }

        return builder;
    }
}
