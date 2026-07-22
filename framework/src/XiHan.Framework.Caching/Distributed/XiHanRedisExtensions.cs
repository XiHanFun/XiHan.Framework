// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using StackExchange.Redis;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// XiHanRedisExtensions
/// </summary>
public static class XiHanRedisExtensions
{
    /// <summary>
    /// 批量获取多个 Redis 哈希表中指定字段的值
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="keys"></param>
    /// <param name="fields"></param>
    /// <returns></returns>
    public static RedisValue[][] HashMemberGetMany(this IDatabase cache, RedisKey[] keys, RedisValue[] fields)
    {
        var tasks = new Task<RedisValue[]>[keys.Length];
        var results = new RedisValue[keys.Length][];

        for (var i = 0; i < keys.Length; i++)
        {
            tasks[i] = cache.HashGetAsync(keys[i], fields);
        }

        for (var i = 0; i < tasks.Length; i++)
        {
            results[i] = cache.Wait(tasks[i]);
        }

        return results;
    }

    /// <summary>
    /// 异步批量获取多个 Redis 哈希表中指定字段的值
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="keys"></param>
    /// <param name="fields"></param>
    /// <returns></returns>
    public static async Task<RedisValue[][]> HashMemberGetManyAsync(this IDatabase cache, RedisKey[] keys, RedisValue[] fields)
    {
        var tasks = new Task<RedisValue[]>[keys.Length];

        for (var i = 0; i < keys.Length; i++)
        {
            tasks[i] = cache.HashGetAsync(keys[i], fields);
        }

        return await Task.WhenAll(tasks);
    }
}
