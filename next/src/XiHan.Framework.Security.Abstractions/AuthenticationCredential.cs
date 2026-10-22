// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 协议层提取的凭据：方案（如 <c>Bearer</c>、<c>ApiKey</c>）与值。方案比较不区分大小写。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuthenticationCredential
{
    /// <summary>
    /// 创建凭据。
    /// </summary>
    public AuthenticationCredential(string scheme, string value)
    {
        Scheme = !string.IsNullOrWhiteSpace(scheme) ? scheme : throw new ArgumentException("The authentication scheme must not be empty.", nameof(scheme));
        Value = !string.IsNullOrEmpty(value) ? value : throw new ArgumentException("The credential value must not be empty.", nameof(value));
    }

    /// <summary>
    /// 方案。
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// 值；不写入日志。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 解析 <c>&lt;scheme&gt; &lt;value&gt;</c> 形式（如 HTTP <c>Authorization</c> 头）。
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? header, [NotNullWhen(true)] out AuthenticationCredential? credential)
    {
        credential = null;
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var trimmed = header.Trim();
        // 分隔符不能在首尾：方案与值都必须非空，"Bearer" 或 " token" 都不算凭据
        var separator = trimmed.IndexOf(' ', StringComparison.Ordinal);
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        var value = trimmed[(separator + 1)..].Trim();
        if (value.Length == 0)
        {
            return false;
        }

        credential = new AuthenticationCredential(trimmed[..separator], value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Scheme} ***";
}
