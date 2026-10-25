// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 跨进程共享的缓存（<c>caching.distributed</c>）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ISharedCache : ICache;
