// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 一次调用的安全上下文
/// </summary>
/// <remarks>
/// 主体与租户。不可变；通过 <see cref="SecurityContextExtensions"/> 读写。主体与租户都是必填的初始化成员（租户可以是 <see langword="null"/>，但要写出来），没有位置构造与解构：以后加字段只加可选的初始化成员，不破坏已有的构造处（ADR-0142、ADR-0288、ADR-0290）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record SecurityContext
{
    /// <summary>
    /// 匿名、无租户
    /// </summary>
    public static SecurityContext Anonymous { get; } = new() { Principal = Principal.Anonymous, Tenant = null };

    /// <summary>
    /// 主体
    /// </summary>
    /// <remarks>
    /// 没有凭据的调用是 <see cref="Security.Principal.Anonymous"/>，不是 <see langword="null"/>
    /// </remarks>
    /// <exception cref="ArgumentNullException">为 <see langword="null"/></exception>
    public required Principal Principal
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 租户
    /// </summary>
    /// <remarks>
    /// 无租户时为 <see langword="null"/>。它是必填成员：没有租户也要写 <c>Tenant = null</c>，换主体时另建上下文就不会忘了带上已有的租户（<c>Tenant = existing.Tenant</c>，或 <c>existing with { Principal = … }</c>）；漏写是编译错误（ADR-0290）
    /// </remarks>
    public required TenantContext? Tenant { get; init; }
}
