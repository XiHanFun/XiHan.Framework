// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 租户 ID 文法：字母或数字开头，之后是字母、数字、<c>.</c>、<c>_</c>、<c>-</c>，不超过 64 个字符；区分大小写。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class TenantIdentifier
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid(string? tenantId) => tenantId is not null && Pattern().IsMatch(tenantId);

    /// <summary>
    /// 校验并返回；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        return IsValid(tenantId)
            ? tenantId
            : throw new ArgumentException($"租户 ID '{tenantId}' 不合法：字母或数字开头，只含字母、数字、'.'、'_'、'-'，不超过 {MaxLength} 个字符。", nameof(tenantId));
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex Pattern();
}
