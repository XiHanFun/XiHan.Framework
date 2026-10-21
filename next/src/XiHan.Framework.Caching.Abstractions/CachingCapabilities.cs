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
    /// <summary>进程内缓存。</summary>
    public const string Local = "caching.local";

    /// <summary>跨进程共享的缓存与分布式锁。</summary>
    public const string Distributed = "caching.distributed";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}

/// <summary>
/// 缓存错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class CachingErrors
{
    /// <summary>键不合法。</summary>
    public const string InvalidKey = "CACHING_VALIDATION_001";

    /// <summary>条目选项不合法。</summary>
    public const string InvalidOptions = "CACHING_VALIDATION_002";

    /// <summary>存储不可达或操作失败。</summary>
    public const string StoreUnavailable = "CACHING_UNAVAILABLE_001";

    /// <summary>锁被其他持有者占用。</summary>
    public const string LockHeld = "CACHING_CONFLICT_001";

    /// <summary>同一幂等键的调用仍在处理中。</summary>
    public const string IdempotentInProgress = "CACHING_CONFLICT_002";
}
