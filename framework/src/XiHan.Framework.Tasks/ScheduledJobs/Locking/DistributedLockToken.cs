#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedLockToken
// Guid:1f184f3d-2e3b-4ddf-b50f-a5f3562d7dbe
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 02:08:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

namespace XiHan.Framework.Tasks.ScheduledJobs.Locking;

/// <summary>
/// 分布式锁令牌
/// </summary>
internal sealed class DistributedLockToken : ILockToken
{
    private const string ReleaseLuaScript = """
                                          if redis.call('get', KEYS[1]) == ARGV[1] then
                                              return redis.call('del', KEYS[1])
                                          end
                                          return 0
                                          """;

    private readonly IDistributedCache<DistributedJobLockCacheItem, string> _distributedCache;
    private readonly ILogger _logger;
    private int _released;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    /// <param name="lockId">锁ID</param>
    /// <param name="distributedCache">分布式缓存</param>
    /// <param name="logger">日志记录器</param>
    public DistributedLockToken(
        string resourceKey,
        string lockId,
        IDistributedCache<DistributedJobLockCacheItem, string> distributedCache,
        ILogger logger)
    {
        ResourceKey = resourceKey;
        LockId = lockId;
        _distributedCache = distributedCache;
        _logger = logger;
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
    public bool IsReleased => Volatile.Read(ref _released) == 1;

    /// <summary>
    /// 释放锁
    /// </summary>
    /// <returns></returns>
    public async Task ReleaseAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) == 1)
        {
            return;
        }

        try
        {
            await _distributedCache.ScriptEvaluateAsync(
                ReleaseLuaScript,
                [ResourceKey],
                [LockId],
                hideErrors: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "释放分布式任务锁失败，资源键: {ResourceKey}, 锁ID: {LockId}",
                ResourceKey,
                LockId);
        }
    }

    /// <summary>
    /// 同步释放锁
    /// </summary>
    public void Dispose()
    {
        ReleaseAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步释放锁
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
    }
}
