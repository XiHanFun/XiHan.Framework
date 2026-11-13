// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Authentication;
using XiHan.Framework.Security.Abstractions.Authorization;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 调用上下文中凭据与授权要求的读写
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityInvocationExtensions
{
    /// <summary>
    /// 调用上下文里存放凭据的键
    /// </summary>
    private static readonly ContextItemKey<AuthenticationCredential> _credentialKey = new("xihan.security.credential");

    /// <summary>
    /// 调用上下文里存放授权要求的键
    /// </summary>
    /// <remarks>
    /// 按写入顺序的数组，每次追加都换成新数组
    /// </remarks>
    private static readonly ContextItemKey<AuthorizationRequirement[]> _requirementsKey = new("xihan.security.requirements");

    /// <summary>
    /// 宿主在 Ingress 阶段放入凭据
    /// </summary>
    public static void SetCredential(this InvocationContext context, AuthenticationCredential credential) => context.Set(_credentialKey, credential);

    /// <summary>
    /// 读取凭据
    /// </summary>
    public static Maybe<AuthenticationCredential> FindCredential(this InvocationContext context) => context.Find(_credentialKey);

    /// <summary>
    /// 追加一条本次调用在 Authorize 阶段必须满足的要求
    /// </summary>
    /// <remarks>
    /// 由宿主按端点元数据写入，也可以由 Authorize 之前的中间件写入；可以调用多次。写入的要求与操作契约推导出的要求叠加判定，全部满足才放行，任何一条都不顶替另一条（ADR-0174）
    /// </remarks>
    public static void RequireAuthorization(this InvocationContext context, AuthorizationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);
        // 没有装配授权能力时没有谁判定它：宁可让调用失败，也不让要求静默失效（ADR-0217）
        if (context.Services.GetService<PolicyCatalog>() is null)
        {
            throw new InvalidOperationException($"The authorization requirement '{requirement.Policy}' cannot be checked: no 'security.authorization@1' provider is assembled.");
        }
        // 复制后追加：上下文条目可能被别的线程读着（ADR-0138），不原地改数组
        var current = context.Find(_requirementsKey).TryGetValue(out var existing) ? existing : [];
        context.Set(_requirementsKey, [.. current, requirement]);
    }

    /// <summary>
    /// 读取已写入的授权要求
    /// </summary>
    /// <remarks>
    /// 按写入顺序，没有时为空；不含操作契约推导出的要求，那一条由 Authorize 阶段自己推导
    /// </remarks>
    public static IReadOnlyList<AuthorizationRequirement> GetAuthorizationRequirements(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Find(_requirementsKey).TryGetValue(out var requirements) ? requirements : [];
    }
}
