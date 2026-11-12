// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tenants;

/// <summary>
/// 租户 ID 文法
/// </summary>
/// <remarks>
/// 小写字母或数字开头，之后是小写字母、数字、<c>.</c>、<c>_</c>、<c>-</c>，不超过 64 个字符，整串匹配。只用小写：租户 ID 要在数据库列、文件路径与主机名里比较，那里往往不区分大小写，只用小写时按序数比较与不区分大小写的比较结论相同（ADR-0196）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class TenantIdentifier
{
    /// <summary>
    /// 最大长度
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// 是否合法
    /// </summary>
    public static bool IsValid(string? tenantId) => tenantId is not null && Pattern().IsMatch(tenantId);

    /// <summary>
    /// 校验并返回
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static string Validate(string tenantId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        return IsValid(tenantId)
            ? tenantId
            : throw new ArgumentException($"Tenant ID '{tenantId}' is invalid: it must start with a lowercase letter or digit, contain only lowercase letters, digits, '.', '_' and '-', and be at most {MaxLength} characters long.", nameof(tenantId));
    }

    /// <summary>
    /// 租户 ID 文法的编译期正则
    /// </summary>
    /// <remarks>
    /// 首字符限定小写字母或数字，总长不超过 <see cref="MaxLength"/>；以 <c>\z</c> 结尾，<c>$</c> 会放过结尾的换行
    /// </remarks>
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}\\z")]
    private static partial Regex Pattern();
}
