// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// Authenticate 阶段的中间件：没有凭据则保持匿名；有凭据则交给方案匹配的 <see cref="IAuthenticationHandler"/>，
/// 成功写入主体，失败以结果返回；<see cref="SecurityOptions.RequireAuthentication"/> 为真时拒绝匿名调用。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class AuthenticationMiddleware(SecurityOptions options) : IPipelineMiddleware
{
    /// <summary>
    /// 安全选项；决定是否拒绝匿名调用。
    /// </summary>
    private readonly SecurityOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        // 先按凭据认证：没有凭据就保持匿名，是否放行交给下游的授权策略
        if (context.GetCredential().TryGetValue(out var credential))
        {
            var handler = FindHandler(context, credential.Scheme);
            if (handler is null)
            {
                context.SetOutcome(Error.Validation(SecurityErrors.UnsupportedScheme, SecurityMessages.UnsupportedScheme, MessageArguments.Of(("scheme", credential.Scheme))));
                return;
            }

            var authenticated = await handler.AuthenticateAsync(credential, context, context.CancellationToken).ConfigureAwait(false);
            if (!authenticated.TryGetValue(out var principal))
            {
                context.SetOutcome(authenticated.ToResult());
                return;
            }

            context.SetSecurity(new SecurityContext(principal, context.GetSecurity().Tenant));
        }

        // 认证之后再检查全局要求：匿名在此被拒，与按策略判定的授权分离
        if (_options.RequireAuthentication && !context.GetSecurity().Principal.IsAuthenticated)
        {
            context.SetOutcome(Error.Denied(SecurityErrors.NotAuthenticated, SecurityMessages.AuthenticationRequired));
            return;
        }

        await nextStep(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 在已注册的处理器里按方案名（不区分大小写）找第一个匹配者；没有返回 <see langword="null"/>。
    /// </summary>
    private static IAuthenticationHandler? FindHandler(InvocationContext context, string scheme)
    {
        foreach (var handler in context.Services.GetServices<IAuthenticationHandler>())
        {
            if (string.Equals(handler.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
            {
                return handler;
            }
        }

        return null;
    }
}
