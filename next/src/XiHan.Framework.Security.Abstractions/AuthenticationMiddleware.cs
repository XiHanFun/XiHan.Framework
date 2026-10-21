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
    private readonly SecurityOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        if (context.GetCredential().TryGetValue(out var credential))
        {
            var handler = FindHandler(context, credential.Scheme);
            if (handler is null)
            {
                context.SetOutcome(Error.Validation(SecurityErrors.UnsupportedScheme, $"不支持的认证方案 '{credential.Scheme}'。"));
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

        if (_options.RequireAuthentication && !context.GetSecurity().Principal.IsAuthenticated)
        {
            context.SetOutcome(Error.Denied(SecurityErrors.NotAuthenticated, "本调用需要认证。"));
            return;
        }

        await nextStep(context).ConfigureAwait(false);
    }

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
