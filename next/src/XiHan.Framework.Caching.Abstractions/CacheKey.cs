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

/// <summary>
/// 条目过期：绝对存活时间或滑动过期，二选一；都不设时用 Provider 的默认存活时间。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record CacheEntryOptions
{
    /// <summary>
    /// 使用 Provider 默认存活时间。
    /// </summary>
    public static CacheEntryOptions Default { get; } = new();

    /// <summary>
    /// 写入后固定存活的时间。
    /// </summary>
    public TimeSpan? TimeToLive
    {
        get => field;
        init => field = ValidatePositive(value, nameof(TimeToLive));
    }

    /// <summary>
    /// 每次读取后延长的时间。
    /// </summary>
    public TimeSpan? SlidingExpiration
    {
        get => field;
        init => field = ValidatePositive(value, nameof(SlidingExpiration));
    }

    /// <summary>
    /// 固定存活。
    /// </summary>
    public static CacheEntryOptions Expires(TimeSpan timeToLive) => new() { TimeToLive = timeToLive };

    /// <summary>
    /// 滑动过期。
    /// </summary>
    public static CacheEntryOptions Sliding(TimeSpan slidingExpiration) => new() { SlidingExpiration = slidingExpiration };

    /// <summary>
    /// 校验组合约束：绝对与滑动不能同时设置。
    /// </summary>
    public CacheEntryOptions Validated()
        => TimeToLive is not null && SlidingExpiration is not null
            ? throw new ArgumentException("TimeToLive 与 SlidingExpiration 不能同时设置。")
            : this;

    private static TimeSpan? ValidatePositive(TimeSpan? value, string name)
        => value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(name, value, "过期时间必须为正。");
}
