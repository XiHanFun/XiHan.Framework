// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 一个条目的大小与存活时间。
/// </summary>
/// <param name="Key">键。</param>
/// <param name="Size">载荷字节数（不含存储自己的头部）。</param>
/// <param name="TimeToLive">剩余存活时间；不过期为 <see langword="null"/>。</param>
/// <param name="SlidingExpiration">滑动窗口；绝对过期为 <see langword="null"/>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CacheEntryInfo(CacheKey Key, long Size, TimeSpan? TimeToLive, TimeSpan? SlidingExpiration);
