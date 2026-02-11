#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryLockProvider
// Guid:82c72445-7344-4f99-b355-4d06e4585069
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 14:27:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

namespace XiHan.Framework.Tasks.ScheduledJobs.Locking;

/// <summary>
/// 内存锁提供者（单机环境）
/// </summary>
public class InMemoryLockProvider : IJobLockProvider
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();

    /// <summary>
    /// 尝试获取锁
    /// </summary>
    public Task<ILockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var lockId = Guid.NewGuid().ToString("N");
        var expiryTime = DateTimeOffset.UtcNow.Add(expiry);

        var entry = new LockEntry
        {
            LockId = lockId,
            ExpiryTime = expiryTime
        };

        // 尝试添加或更新锁
        var acquired = _locks.TryAdd(resourceKey, entry) ||
                      (_locks.TryGetValue(resourceKey, out var existing) &&
                       existing.IsExpired() &&
                       _locks.TryUpdate(resourceKey, entry, existing));

        if (acquired)
        {
            var token = new InMemoryLockToken(resourceKey, lockId, this);
            return Task.FromResult<ILockToken?>(token);
        }

        return Task.FromResult<ILockToken?>(null);
    }

    /// <summary>
    /// 释放锁
    /// </summary>
    internal void ReleaseLock(string resourceKey, string lockId)
    {
        if (_locks.TryGetValue(resourceKey, out var entry) && entry.LockId == lockId)
        {
            _locks.TryRemove(resourceKey, out _);
        }
    }

    /// <summary>
    /// 锁条目
    /// </summary>
    private class LockEntry
    {
        public string LockId { get; set; } = string.Empty;
        public DateTimeOffset ExpiryTime { get; set; }

        public bool IsExpired() => DateTimeOffset.UtcNow >= ExpiryTime;
    }
}
