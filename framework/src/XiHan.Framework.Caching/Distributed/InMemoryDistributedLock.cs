// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// 进程内分布式锁回退实现（Redis 未启用时使用）。
/// </summary>
/// <remarks>
/// 仅在当前进程内互斥，<b>不跨实例</b>；多实例部署务必启用 Redis 改用 <see cref="RedisDistributedLock"/>。
/// </remarks>
public sealed class InMemoryDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<IDistributedLockHandle?> TryAcquireAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "锁过期时间必须大于零。");
        }

        var key = resourceKey.Trim();
        var entry = new LockEntry(Guid.NewGuid().ToString("N"), DateTime.UtcNow.Add(expiry).Ticks);

        while (true)
        {
            if (_locks.TryGetValue(key, out var existing))
            {
                // 仍在有效期内 → 获取失败
                if (existing.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks)
                {
                    return Task.FromResult<IDistributedLockHandle?>(null);
                }

                // 已过期 → 原子接管（CAS 旧条目）
                if (!_locks.TryUpdate(key, entry, existing))
                {
                    continue;
                }
            }
            else if (!_locks.TryAdd(key, entry))
            {
                continue;
            }

            return Task.FromResult<IDistributedLockHandle?>(new InMemoryDistributedLockHandle(_locks, key, entry));
        }
    }

    internal sealed class LockEntry(string lockId, long expiresAtUtcTicks)
    {
        public string LockId { get; } = lockId;

        public long ExpiresAtUtcTicks { get; set; } = expiresAtUtcTicks;
    }
}

/// <summary>
/// 进程内分布式锁句柄。
/// </summary>
internal sealed class InMemoryDistributedLockHandle : IDistributedLockHandle
{
    private readonly ConcurrentDictionary<string, InMemoryDistributedLock.LockEntry> _locks;
    private readonly InMemoryDistributedLock.LockEntry _entry;
    private int _released;

    public InMemoryDistributedLockHandle(
        ConcurrentDictionary<string, InMemoryDistributedLock.LockEntry> locks,
        string resourceKey,
        InMemoryDistributedLock.LockEntry entry)
    {
        _locks = locks;
        _entry = entry;
        ResourceKey = resourceKey;
        LockId = entry.LockId;
    }

    public string ResourceKey { get; }

    public string LockId { get; }

    public bool IsReleased => Volatile.Read(ref _released) != 0;

    public Task ReleaseAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            // 仅当字典里仍是本句柄持有的条目时删除（引用相等），避免删掉接管者的锁
            _locks.TryRemove(new KeyValuePair<string, InMemoryDistributedLock.LockEntry>(ResourceKey, _entry));
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExtendAsync(TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "锁过期时间必须大于零。");
        }

        if (!IsReleased && _locks.TryGetValue(ResourceKey, out var current) && ReferenceEquals(current, _entry))
        {
            _entry.ExpiresAtUtcTicks = DateTime.UtcNow.Add(expiry).Ticks;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public void Dispose()
    {
        ReleaseAsync().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        _ = ReleaseAsync();
        return ValueTask.CompletedTask;
    }
}
