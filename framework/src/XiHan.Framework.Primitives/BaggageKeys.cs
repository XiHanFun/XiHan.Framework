// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 随调用跨进程传播的 Baggage 键
/// </summary>
/// <remarks>
/// 键名由 <c>spec/conventions/baggage-keys.md</c> 登记，格式 <c>xihan.&lt;name&gt;</c>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class BaggageKeys
{
    /// <summary>
    /// 当前租户 ID
    /// </summary>
    public const string Tenant = "xihan.tenant";

    /// <summary>
    /// 当前主体 ID
    /// </summary>
    public const string Principal = "xihan.principal";

    /// <summary>
    /// 业务关联 ID
    /// </summary>
    public const string Correlation = "xihan.correlation";

    /// <summary>
    /// 幂等键
    /// </summary>
    public const string IdempotencyKey = "xihan.idempotency-key";

    /// <summary>
    /// 请求文化（BCP 47）
    /// </summary>
    public const string Locale = "xihan.locale";

    /// <summary>
    /// 已登记的全部键
    /// </summary>
    public static IReadOnlyList<string> Registered { get; } = [Tenant, Principal, Correlation, IdempotencyKey, Locale];

    /// <summary>
    /// 值的最大长度（字符）
    /// </summary>
    /// <remarks>
    /// 租户 ID、主体 ID、关联 ID 与区域都远短于它；W3C Baggage 整个头的建议上限是 8192 字节（ADR-0204）
    /// </remarks>
    public const int MaxValueLength = 256;

    /// <summary>
    /// 值是否可以进入调用上下文
    /// </summary>
    /// <remarks>
    /// 非空、不超过 <see cref="MaxValueLength"/>、不含控制字符：这些值会写进日志作用域、审计与消息信封，换行能伪造日志行（ADR-0204）
    /// </remarks>
    public static bool IsAcceptableValue(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxValueLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 键名是否符合 <c>xihan.&lt;name&gt;</c> 格式（小写字母、数字、连字符）
    /// </summary>
    public static bool IsWellFormed(string? key) => key is not null && Pattern().IsMatch(key);

    /// <summary>
    /// 键名是否已登记
    /// </summary>
    /// <remarks>
    /// 载体的入站只收已登记的键，出站只写已登记的键；未登记的键只在进程内可用（RFC-0003 规则 3）
    /// </remarks>
    public static bool IsRegistered(string? key) => key is not null && Registered.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// 键名格式的正则：<c>xihan.</c> 前缀，其后为小写字母开头的小写字母、数字、连字符
    /// </summary>
    [GeneratedRegex("^xihan\\.[a-z][a-z0-9-]*$")]
    private static partial Regex Pattern();
}
