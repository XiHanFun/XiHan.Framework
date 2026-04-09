#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryUpgradeLockProvider
// Guid:90ee8563-2ec2-477b-8924-37f3209c161a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Upgrade.Abstractions;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 基于内存的升级锁提供者（默认实现）
/// </summary>
public class InMemoryUpgradeLockProvider : IUpgradeLockProvider
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new(StringComparer.Ordinal);

    /// <summary>
    /// 尝试获取锁
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">锁过期时间</param>
    /// <param name="nodeName">节点名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁令牌，获取失败返回 null</returns>
    public Task<IUpgradeLockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, string nodeName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            throw new ArgumentException("升级锁资源键不能为空。", nameof(resourceKey));
        }

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "升级锁过期时间必须大于 0。");
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = DateTimeOffset.UtcNow;
            var lockId = Guid.NewGuid().ToString("N");
            var entry = new LockEntry(lockId, nodeName, now.Add(expiry));

            if (!_locks.TryGetValue(resourceKey, out var existing))
            {
                if (_locks.TryAdd(resourceKey, entry))
                {
                    return Task.FromResult<IUpgradeLockToken?>(new InMemoryUpgradeLockToken(resourceKey, lockId, this));
                }

                continue;
            }

            if (!existing.IsExpired(now))
            {
                return Task.FromResult<IUpgradeLockToken?>(null);
            }

            if (_locks.TryUpdate(resourceKey, entry, existing))
            {
                return Task.FromResult<IUpgradeLockToken?>(new InMemoryUpgradeLockToken(resourceKey, lockId, this));
            }
        }

        return Task.FromResult<IUpgradeLockToken?>(null);
    }

    /// <summary>
    /// 释放锁
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    /// <param name="lockId">锁标识</param>
    internal void Release(string resourceKey, string lockId)
    {
        if (_locks.TryGetValue(resourceKey, out var entry) &&
            string.Equals(entry.LockId, lockId, StringComparison.Ordinal))
        {
            _locks.TryRemove(resourceKey, out _);
        }
    }

    /// <summary>
    /// 锁条目
    /// </summary>
    /// <param name="LockId">锁标识</param>
    /// <param name="NodeName">节点名称</param>
    /// <param name="ExpiryTime">过期时间</param>
    private sealed record LockEntry(string LockId, string NodeName, DateTimeOffset ExpiryTime)
    {
        /// <summary>
        /// 是否过期
        /// </summary>
        /// <param name="now">当前时间</param>
        /// <returns>是否过期</returns>
        public bool IsExpired(DateTimeOffset now)
        {
            return now >= ExpiryTime;
        }
    }

    /// <summary>
    /// 内存升级锁令牌
    /// </summary>
    private sealed class InMemoryUpgradeLockToken : IUpgradeLockToken
    {
        private readonly InMemoryUpgradeLockProvider _provider;
        private int _isReleased;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="resourceKey">资源键</param>
        /// <param name="lockId">锁标识</param>
        /// <param name="provider">锁提供者</param>
        public InMemoryUpgradeLockToken(string resourceKey, string lockId, InMemoryUpgradeLockProvider provider)
        {
            ResourceKey = resourceKey;
            LockId = lockId;
            _provider = provider;
        }

        /// <summary>
        /// 资源键
        /// </summary>
        public string ResourceKey { get; }

        /// <summary>
        /// 锁标识
        /// </summary>
        public string LockId { get; }

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsReleased => Volatile.Read(ref _isReleased) == 1;

        /// <summary>
        /// 释放锁
        /// </summary>
        /// <returns>任务</returns>
        public Task ReleaseAsync()
        {
            if (Interlocked.Exchange(ref _isReleased, 1) == 1)
            {
                return Task.CompletedTask;
            }

            _provider.Release(ResourceKey, LockId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异步释放锁
        /// </summary>
        /// <returns>任务</returns>
        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
        }
    }
}
