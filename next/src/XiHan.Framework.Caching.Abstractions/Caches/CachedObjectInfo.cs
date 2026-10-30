// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 一个对象条目的类型与存活时间。
/// </summary>
/// <param name="Key">键。</param>
/// <param name="TypeName">条目里对象的类型全名。</param>
/// <param name="TimeToLive">剩余存活时间；不过期为 <see langword="null"/>。</param>
/// <param name="SlidingExpiration">滑动窗口；绝对过期为 <see langword="null"/>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CachedObjectInfo(CacheKey Key, string TypeName, TimeSpan? TimeToLive, TimeSpan? SlidingExpiration);
