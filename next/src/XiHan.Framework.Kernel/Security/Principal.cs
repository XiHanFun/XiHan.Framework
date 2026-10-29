// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 当前操作的主体。
/// </summary>
/// <remarks>
/// 用户、服务或系统。不可变；由 Authenticate 阶段写入上下文。
/// </remarks>
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
    /// 声明集合。
    /// </summary>
    /// <remarks>
    /// 同一类型可有多个值。
    /// </remarks>
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
