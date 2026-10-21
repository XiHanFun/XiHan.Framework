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

/// <summary>
/// 租户信息。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record TenantInfo
{
    private static readonly IReadOnlyDictionary<string, string> NoProperties = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 租户 ID。
    /// </summary>
    public required string Id
    {
        get => field;
        init => field = TenantIdentifier.Validate(value);
    }

    /// <summary>
    /// 显示名。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 是否启用；禁用的租户解析时被拒绝。默认启用。
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 租户专属属性（连接、配额等），键区分大小写。
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = NoProperties;
}
