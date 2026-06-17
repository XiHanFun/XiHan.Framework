#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RedisDistributedLock
// Guid:4a5b6c7d-8e9f-4ab1-b2c3-4d5e6f7a8b9c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using StackExchange.Redis;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// 基于 Redis 的分布式锁：StackExchange.Redis 内置 <c>LockTake</c>（SET NX PX）/ <c>LockRelease</c>（校验持有者后删除）。
/// </summary>
public sealed class RedisDistributedLock : IDistributedLock
{
    private readonly IConnectionMultiplexer _connection;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RedisDistributedLock(IConnectionMultiplexer connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<IDistributedLockHandle?> TryAcquireAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "锁过期时间必须大于零。");
        }

        var key = (RedisKey)$"xihan:lock:{resourceKey.Trim()}";
        var lockId = Guid.NewGuid().ToString("N");
        var db = _connection.GetDatabase();

        var acquired = await db.LockTakeAsync(key, lockId, expiry);
        return acquired ? new RedisDistributedLockHandle(db, key, resourceKey.Trim(), lockId) : null;
    }
}

/// <summary>
/// Redis 分布式锁句柄。
/// </summary>
internal sealed class RedisDistributedLockHandle : IDistributedLockHandle
{
    private readonly IDatabase _db;
    private readonly RedisKey _key;
    private int _released;

    public RedisDistributedLockHandle(IDatabase db, RedisKey key, string resourceKey, string lockId)
    {
        _db = db;
        _key = key;
        ResourceKey = resourceKey;
        LockId = lockId;
    }

    public string ResourceKey { get; }

    public string LockId { get; }

    public bool IsReleased => Volatile.Read(ref _released) != 0;

    public async Task ReleaseAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        // 仅删除自己持有的锁（内置 Lua 校验 value 一致），避免误删续期/重入后他人的锁
        await _db.LockReleaseAsync(_key, LockId);
    }

    public Task<bool> ExtendAsync(TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "锁过期时间必须大于零。");
        }

        return _db.LockExtendAsync(_key, LockId, expiry);
    }

    public void Dispose()
    {
        ReleaseAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
    }
}
