// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 调用上下文中安全上下文的读写
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityContextExtensions
{
    /// <summary>
    /// 安全上下文在 <see cref="InvocationContext.Items"/> 中的键
    /// </summary>
    private static readonly ContextItemKey<SecurityContext> _key = new("xihan.security");

    /// <summary>
    /// 「Tenant 阶段已裁定这次调用的租户」在 <see cref="InvocationContext.Items"/> 中的键
    /// </summary>
    private static readonly ContextItemKey<bool> _tenantResolvedKey = new("xihan.security.tenant-resolved");

    /// <summary>
    /// 「这是有意不带租户的平台调用」在 <see cref="InvocationContext.Items"/> 中的键
    /// </summary>
    private static readonly ContextItemKey<bool> _platformKey = new("xihan.security.platform");

    /// <summary>
    /// 读取安全上下文
    /// </summary>
    /// <remarks>
    /// 未设置时为 <see cref="SecurityContext.Anonymous"/>
    /// </remarks>
    public static SecurityContext GetSecurity(this InvocationContext context) => context.Find(_key).ValueOr(SecurityContext.Anonymous);

    /// <summary>
    /// 写入安全上下文
    /// </summary>
    /// <remarks>
    /// Authenticate 与 Tenant 阶段调用；之后的阶段不应再改
    /// </remarks>
    public static void SetSecurity(this InvocationContext context, SecurityContext security) => context.Set(_key, security);

    /// <summary>
    /// 只替换租户，保留主体
    /// </summary>
    public static void SetTenant(this InvocationContext context, TenantContext? tenant)
        => context.Set(_key, context.GetSecurity() with { Tenant = tenant });

    /// <summary>
    /// 记下 Tenant 阶段已裁定这次调用的租户
    /// </summary>
    /// <remarks>
    /// Tenant 阶段一进入就调用，不论之后解析出租户、裁定为无租户还是拒绝调用：此后安全上下文里的租户就是这次调用唯一可信的租户，观察者（审计等）不再回退到调用方自报的 Baggage（ADR-0191）
    /// </remarks>
    public static void MarkTenantResolved(this InvocationContext context) => context.Set(_tenantResolvedKey, true);

    /// <summary>
    /// Tenant 阶段是否已裁定这次调用的租户
    /// </summary>
    /// <remarks>
    /// 没有装配多租户（没有 Tenant 阶段）时为 <see langword="false"/>
    /// </remarks>
    public static bool IsTenantResolved(this InvocationContext context) => context.Find(_tenantResolvedKey).ValueOr(false);

    /// <summary>
    /// 声明这次调用是有意不带租户的平台调用
    /// </summary>
    /// <remarks>
    /// 由进程内开调用的代码在交给管道之前声明（如工作流为平台分区开的调用），调用方无法经 Baggage 或请求声明它。Tenant 阶段对平台调用不问解析器、不补缺省租户、不要求带租户，租户保持为空（ADR-0192）
    /// </remarks>
    public static void MarkPlatformCall(this InvocationContext context) => context.Set(_platformKey, true);

    /// <summary>
    /// 这次调用是否声明为平台调用
    /// </summary>
    public static bool IsPlatformCall(this InvocationContext context) => context.Find(_platformKey).ValueOr(false);
}
