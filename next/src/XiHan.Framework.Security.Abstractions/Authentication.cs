// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 协议层提取的凭据：方案（如 <c>Bearer</c>、<c>ApiKey</c>）与值。方案比较不区分大小写。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuthenticationCredential
{
    /// <summary>
    /// 创建凭据。
    /// </summary>
    public AuthenticationCredential(string scheme, string value)
    {
        Scheme = !string.IsNullOrWhiteSpace(scheme) ? scheme : throw new ArgumentException("认证方案不能为空。", nameof(scheme));
        Value = !string.IsNullOrEmpty(value) ? value : throw new ArgumentException("凭据值不能为空。", nameof(value));
    }

    /// <summary>
    /// 方案。
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// 值；不写入日志。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 解析 <c>&lt;scheme&gt; &lt;value&gt;</c> 形式（如 HTTP <c>Authorization</c> 头）。
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? header, [NotNullWhen(true)] out AuthenticationCredential? credential)
    {
        credential = null;
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var trimmed = header.Trim();
        var separator = trimmed.IndexOf(' ', StringComparison.Ordinal);
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        var value = trimmed[(separator + 1)..].Trim();
        if (value.Length == 0)
        {
            return false;
        }

        credential = new AuthenticationCredential(trimmed[..separator], value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Scheme} ***";
}

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

/// <summary>
/// 调用上下文中凭据与授权要求的读写。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityInvocationExtensions
{
    private static readonly ContextItemKey<AuthenticationCredential> CredentialKey = new("xihan.security.credential");
    private static readonly ContextItemKey<AuthorizationRequirement> RequirementKey = new("xihan.security.requirement");

    /// <summary>
    /// 宿主在 Ingress 阶段放入凭据。
    /// </summary>
    public static void SetCredential(this InvocationContext context, AuthenticationCredential credential) => context.Set(CredentialKey, credential);

    /// <summary>
    /// 读取凭据。
    /// </summary>
    public static Maybe<AuthenticationCredential> GetCredential(this InvocationContext context) => context.Get(CredentialKey);

    /// <summary>
    /// 声明本次调用在 Authorize 阶段必须满足的要求；由宿主端点元数据或操作契约设置。
    /// </summary>
    public static void RequireAuthorization(this InvocationContext context, AuthorizationRequirement requirement) => context.Set(RequirementKey, requirement);

    /// <summary>
    /// 读取授权要求。
    /// </summary>
    public static Maybe<AuthorizationRequirement> GetAuthorizationRequirement(this InvocationContext context) => context.Get(RequirementKey);
}

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
