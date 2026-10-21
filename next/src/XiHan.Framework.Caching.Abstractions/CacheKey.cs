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
            : throw new ArgumentException($"缓存键 '{value}' 不合法：必须非空、不含空白与控制字符、不超过 {MaxLength} 个字符。", nameof(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
