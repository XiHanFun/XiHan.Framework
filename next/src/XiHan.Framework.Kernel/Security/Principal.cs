// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 当前操作的主体：用户、服务或系统。不可变；由 Authenticate 阶段写入上下文。
/// </summary>
/// <param name="Id">主体 ID；匿名主体为空字符串。</param>
/// <param name="AuthenticationScheme">认证方案；匿名主体为 <see langword="null"/>。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record Principal(string Id, string? AuthenticationScheme)
{
    /// <summary>
    /// 匿名主体。
    /// </summary>
    public static Principal Anonymous { get; } = new(string.Empty, null);

    /// <summary>
    /// 显示名。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 声明集合；同一类型可有多个值。
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Claims { get; init; } = [];

    /// <summary>
    /// 是否已认证。
    /// </summary>
    public bool IsAuthenticated => AuthenticationScheme is not null && Id.Length > 0;

    /// <summary>
    /// 取指定类型的全部声明值。
    /// </summary>
    public IEnumerable<string> ClaimValues(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Claims.Where(c => string.Equals(c.Key, type, StringComparison.Ordinal)).Select(static c => c.Value);
    }

    /// <summary>
    /// 是否拥有指定类型与值的声明。
    /// </summary>
    public bool HasClaim(string type, string value)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(value);
        return Claims.Any(c => string.Equals(c.Key, type, StringComparison.Ordinal) && string.Equals(c.Value, value, StringComparison.Ordinal));
    }
}

/// <summary>
/// 当前租户。由 Tenant 阶段写入上下文。
/// </summary>
/// <param name="Id">租户 ID。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record TenantContext(string Id)
{
    /// <summary>
    /// 租户 ID。
    /// </summary>
    public string Id { get; } = string.IsNullOrWhiteSpace(Id) ? throw new ArgumentException("租户 ID 不能为空。", nameof(Id)) : Id;

    /// <summary>
    /// 显示名。
    /// </summary>
    public string? DisplayName { get; init; }
}

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
