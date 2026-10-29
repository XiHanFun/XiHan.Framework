// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;
using CredentialHandler = XiHan.Framework.Security.Abstractions.Authentication.IAuthenticationHandler;

namespace XiHan.Framework.Hosting.AspNetCore.Authentication;

/// <summary>
/// 认证方案 <c>XiHan</c> 的处理器。
/// </summary>
/// <remarks>
/// 凭据按 <see cref="HttpInvocation.CreateContext(HttpContext, IServiceProvider)"/> 的同一规则取（<c>Authorization</c> 头，或登记路径下的查询串令牌），以本次请求建调用上下文，交给方案匹配的内核 <see cref="CredentialHandler"/>（与管道的 Authenticate 阶段同一个处理器），再依次过 <see cref="IInvocationAuthenticationFilter"/>。只认带 <see cref="PipelineBypassMetadata"/> 的端点（其余请求由管道认证，这里为未认证）；没有凭据为未认证；方案不支持、凭据无效或挂点拒绝为认证失败，质询与管道一致（401 带 <c>WWW-Authenticate</c>）。主体转成 <see cref="ClaimsPrincipal"/>：声明按原始类型名保留，另加 <see cref="ClaimTypes.NameIdentifier"/> 承载主体 ID 并作为名称声明，角色声明类型取 <see cref="SecurityOptions.RoleClaim"/>。
/// </remarks>
/// <param name="options">方案选项。</param>
/// <param name="logger">日志工厂。</param>
/// <param name="encoder">URL 编码器。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InvocationAuthenticationHandler(IOptionsMonitor<InvocationAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<InvocationAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>
    /// 本次请求出示的凭据。
    /// </summary>
    /// <remarks>
    /// 质询时用。
    /// </remarks>
    private Maybe<AuthenticationCredential> _presented;

    /// <summary>
    /// 本次请求认证失败的错误。
    /// </summary>
    /// <remarks>
    /// 质询时用。
    /// </remarks>
    private Error? _failure;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 只认绕过管道的端点：只登记了这一个方案时它会成为缺省方案，普通请求由管道认证，这里不再认证一遍
        if (Context.GetEndpoint()?.Metadata.GetMetadata<PipelineBypassMetadata>() is null)
        {
            return AuthenticateResult.NoResult();
        }

        _presented = HttpInvocation.CredentialOf(Context);
        if (!_presented.TryGetValue(out var credential))
        {
            return AuthenticateResult.NoResult();
        }

        var services = Context.RequestServices;
        var handler = services.GetServices<CredentialHandler>().FirstOrDefault(h => string.Equals(h.Scheme, credential.Scheme, StringComparison.OrdinalIgnoreCase));
        if (handler is null)
        {
            return Fail(Error.Validation(SecurityErrors.UnsupportedScheme, SecurityMessages.UnsupportedScheme, MessageArguments.Of(("scheme", credential.Scheme))));
        }

        // 与管道同一套载体映射：链路、区域、来源与凭据；不记录到 HttpContext.Items，不影响之后经过 UseXiHan() 的同一请求
        var context = HttpInvocation.CreateContext(Context, services);
        var authenticated = await handler.AuthenticateAsync(credential, context, Context.RequestAborted).ConfigureAwait(false);
        if (!authenticated.TryGetValue(out var principal))
        {
            return Fail(authenticated.Error!);
        }

        context.SetSecurity(new SecurityContext(principal, context.GetSecurity().Tenant));
        foreach (var filter in services.GetServices<IInvocationAuthenticationFilter>())
        {
            var checkedPrincipal = await filter.AuthenticatedAsync(principal, context, Context.RequestAborted).ConfigureAwait(false);
            if (checkedPrincipal.IsFailure)
            {
                return Fail(checkedPrincipal.Error!);
            }
        }

        var identity = new ClaimsIdentity(Scheme.Name, ClaimTypes.NameIdentifier, services.GetRequiredService<SecurityOptions>().RoleClaim);
        foreach (var (type, value) in principal.Claims)
        {
            identity.AddClaim(new Claim(type, value));
        }

        if (!identity.HasClaim(ClaimTypes.NameIdentifier, principal.Id))
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, principal.Id));
        }

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        // 不是认证失败的错误（方案不支持）与没有凭据一样，按已装配的方案逐个质询
        var error = _failure is not null && ProblemResponse.IsAuthenticationFailure(_failure)
            ? _failure
            : Error.Denied(SecurityErrors.NotAuthenticated, SecurityMessages.AuthenticationRequired);
        var presented = ReferenceEquals(error, _failure) ? _presented : Maybe.None<AuthenticationCredential>();
        var schemes = Context.RequestServices.GetServices<CredentialHandler>().Select(static h => h.Scheme);
        foreach (var challenge in AuthenticationChallenge.For(error, presented, schemes))
        {
            Response.Headers.Append(HeaderNames.WWWAuthenticate, challenge);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 记下错误并返回失败。
    /// </summary>
    /// <remarks>
    /// 失败原因只进日志，响应只有状态码与质询。
    /// </remarks>
    private AuthenticateResult Fail(Error error)
    {
        _failure = error;
        return AuthenticateResult.Fail(error.Code + ": " + error.Message);
    }
}
