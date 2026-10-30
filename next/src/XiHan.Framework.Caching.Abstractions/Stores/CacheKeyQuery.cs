// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 键列表的查询。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CacheKeyQuery
{
    /// <summary>
    /// 每页上限的最大值。
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// 键的前缀。
    /// </summary>
    /// <remarks>
    /// 空为全部；与键同文法（无空白与控制字符，不超过 <see cref="CacheKey.MaxLength"/>），区分大小写。
    /// </remarks>
    public string Prefix
    {
        get => field;
        init => field = CacheKey.IsValidPrefix(value) ? value : throw new ArgumentException($"Cache key prefix '{value}' is invalid: it must contain no whitespace or control characters and be at most {CacheKey.MaxLength} characters long.", nameof(value));
    } = string.Empty;

    /// <summary>
    /// 续页标记。
    /// </summary>
    /// <remarks>
    /// 上一页的 <see cref="CacheKeyPage.Continuation"/>；第一页为 <see langword="null"/>。
    /// </remarks>
    public string? Continuation { get; init; }

    /// <summary>
    /// 每页上限，1 到 <see cref="MaxLimit"/>。
    /// </summary>
    /// <remarks>
    /// 默认 100；Redis 实现把它作为 SCAN 的提示值。
    /// </remarks>
    public int Limit
    {
        get => field;
        init => field = value is >= 1 and <= MaxLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The limit must be between 1 and {MaxLimit}.");
    } = 100;
}
