// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 缓存子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class CachingCapabilities
{
    /// <summary>
    /// 进程内缓存。
    /// </summary>
    public const string Local = "caching.local";

    /// <summary>
    /// 跨进程共享的缓存与分布式锁。
    /// </summary>
    public const string Distributed = "caching.distributed";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
