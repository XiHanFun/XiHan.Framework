// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 一次调用的安全上下文：主体与租户。不可变；通过 <see cref="SecurityContextExtensions"/> 读写。
/// </summary>
/// <param name="Principal">主体。</param>
/// <param name="Tenant">租户；无租户时为 <see langword="null"/>。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record SecurityContext(Principal Principal, TenantContext? Tenant)
{
    /// <summary>
    /// 匿名、无租户。
    /// </summary>
    public static SecurityContext Anonymous { get; } = new(Principal.Anonymous, null);

    /// <summary>
    /// 主体。
    /// </summary>
    public Principal Principal { get; } = Principal ?? throw new ArgumentNullException(nameof(Principal));
}
