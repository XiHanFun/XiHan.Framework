// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 缓存键：非空、不含空白与控制字符、不超过 <see cref="MaxLength"/>。区分大小写。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct CacheKey
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 512;

    /// <summary>
    /// 创建键；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public CacheKey(string value)
    {
        Value = Validate(value);
    }

    /// <summary>
    /// 键文本。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 从字符串隐式创建。
    /// </summary>
    public static implicit operator CacheKey(string value) => new(value);

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        // 逐字符拒绝空白与控制字符：键会出现在日志、协议行与前缀拼接里，这些字符会造成截断或歧义
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 校验并返回；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsValid(value)
            ? value
            : throw new ArgumentException($"Cache key '{value}' is invalid: it must be non-empty, contain no whitespace or control characters, and be at most {MaxLength} characters long.", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
