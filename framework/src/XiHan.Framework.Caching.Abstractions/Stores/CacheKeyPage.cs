// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 键列表的一页
/// </summary>
/// <remarks>
/// 成员经初始化器给出，与其他续页分页同名（ADR-0154）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CacheKeyPage
{
    /// <summary>
    /// 本页的键
    /// </summary>
    public required IReadOnlyList<CacheKey> Items { get; init; }

    /// <summary>
    /// 下一页的续页标记
    /// </summary>
    /// <remarks>
    /// 没有更多为 <see langword="null"/>
    /// </remarks>
    public string? ContinuationToken { get; init; }
}
