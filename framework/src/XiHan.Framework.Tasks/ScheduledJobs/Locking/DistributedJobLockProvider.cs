#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedJobLockProvider
// Guid:2a57ee42-f409-48a2-ae8f-7ec02e71353a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 02:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

namespace XiHan.Framework.Tasks.ScheduledJobs.Locking;

/// <summary>
/// 分布式任务锁提供者（基于 Redis Lua 脚本）
/// </summary>
public class DistributedJobLockProvider : IJobLockProvider
{
    private const string AcquireLuaScript = """
                                          if redis.call('exists', KEYS[1]) == 0 then
                                              redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2])
                                              return 1
                                          end
                                          return 0
                                          """;

    private readonly IDistributedCache<DistributedJobLockCacheItem, string> _distributedCache;
    private readonly ILogger<DistributedJobLockProvider> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="distributedCache">分布式缓存</param>
    /// <param name="logger">日志记录器</param>
    public DistributedJobLockProvider(
        IDistributedCache<DistributedJobLockCacheItem, string> distributedCache,
        ILogger<DistributedJobLockProvider> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    /// <summary>
    /// 尝试获取锁
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁令牌</returns>
    public async Task<ILockToken?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "锁过期时间必须大于零。");
        }

        var lockId = Guid.NewGuid().ToString("N");
        var expiryMilliseconds = Math.Max(1L, (long)expiry.TotalMilliseconds);

        try
        {
            var result = await _distributedCache.ScriptEvaluateAsync(
                AcquireLuaScript,
                [resourceKey.Trim()],
                [lockId, expiryMilliseconds],
                hideErrors: false,
                token: cancellationToken);

            if (!IsAcquired(result))
            {
                return null;
            }

            return new DistributedLockToken(resourceKey.Trim(), lockId, _distributedCache, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "尝试获取分布式任务锁失败，资源键: {ResourceKey}",
                resourceKey);

            return null;
        }
    }

    /// <summary>
    /// 判断锁是否获取成功
    /// </summary>
    /// <param name="result">脚本执行结果</param>
    /// <returns>成功返回 true</returns>
    private static bool IsAcquired(RedisResult? result)
    {
        if (result is null)
        {
            return false;
        }

        var responseText = result.ToString();

        return result.Resp2Type switch
        {
            ResultType.Integer => (long)result == 1,
            ResultType.SimpleString => string.Equals(responseText, "OK", StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(responseText, "1", StringComparison.Ordinal)
        };
    }
}
