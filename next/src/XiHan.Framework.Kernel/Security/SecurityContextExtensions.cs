// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 调用上下文中安全上下文的读写。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class SecurityContextExtensions
{
    private static readonly ContextItemKey<SecurityContext> _key = new("xihan.security");

    /// <summary>
    /// 读取安全上下文；未设置时为 <see cref="SecurityContext.Anonymous"/>。
    /// </summary>
    public static SecurityContext GetSecurity(this InvocationContext context) => context.Get(_key).ValueOr(SecurityContext.Anonymous);

    /// <summary>
    /// 写入安全上下文。Authenticate 与 Tenant 阶段调用；之后的阶段不应再改。
    /// </summary>
    public static void SetSecurity(this InvocationContext context, SecurityContext security) => context.Set(_key, security);

    /// <summary>
    /// 只替换租户，保留主体。
    /// </summary>
    public static void SetTenant(this InvocationContext context, TenantContext? tenant)
        => context.Set(_key, context.GetSecurity() with { Tenant = tenant });
}
