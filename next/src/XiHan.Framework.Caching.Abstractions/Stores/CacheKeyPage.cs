// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 一页键。
/// </summary>
/// <param name="Keys">本页的键。</param>
/// <param name="Continuation">下一页的续页标记；没有更多为 <see langword="null"/>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CacheKeyPage(IReadOnlyList<CacheKey> Keys, string? Continuation);
