// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 调用上下文中凭据与授权要求的读写。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityInvocationExtensions
{
    /// <summary>
    /// 调用上下文里存放凭据的键。
    /// </summary>
    private static readonly ContextItemKey<AuthenticationCredential> _credentialKey = new("xihan.security.credential");

    /// <summary>
    /// 调用上下文里存放授权要求的键。
    /// </summary>
    private static readonly ContextItemKey<AuthorizationRequirement> _requirementKey = new("xihan.security.requirement");

    /// <summary>
    /// 宿主在 Ingress 阶段放入凭据。
    /// </summary>
    public static void SetCredential(this InvocationContext context, AuthenticationCredential credential) => context.Set(_credentialKey, credential);

    /// <summary>
    /// 读取凭据。
    /// </summary>
    public static Maybe<AuthenticationCredential> GetCredential(this InvocationContext context) => context.Get(_credentialKey);

    /// <summary>
    /// 声明本次调用在 Authorize 阶段必须满足的要求；由宿主端点元数据或操作契约设置。
    /// </summary>
    public static void RequireAuthorization(this InvocationContext context, AuthorizationRequirement requirement) => context.Set(_requirementKey, requirement);

    /// <summary>
    /// 读取授权要求。
    /// </summary>
    public static Maybe<AuthorizationRequirement> GetAuthorizationRequirement(this InvocationContext context) => context.Get(_requirementKey);
}
