#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanRedisCache
// Guid:6dce35b8-70f1-4853-8c62-616f9b6bf008
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 04:54:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Buffers;
using System.Reflection;
using System.Text;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// 曦寒 Redis 缓存
/// </summary>
[DisableConventionalRegistration]
public class XiHanRedisCache : RedisCache, ICacheSupportsMultipleItems, ICacheSupportsKeyPattern, ICacheSupportsLuaScript
{
    /// <summary>
    /// Redis 扫描键页大小
    /// </summary>
    protected const int DefaultKeyScanPageSize = 500;

    /// <summary>
    /// 绝对过期时间键
    /// </summary>
    protected static readonly string AbsoluteExpirationKey;

    /// <summary>
    /// 滑动过期时间键
    /// </summary>
    protected static readonly string SlidingExpirationKey;

    /// <summary>
    /// 数据键
    /// </summary>
    protected static readonly string DataKey;

    /// <summary>
    /// 不存在
    /// </summary>
    protected static readonly long NotPresent;

    /// <summary>
    /// 绝对过期时间键、滑动过期时间键、数据键
    /// </summary>
    protected static readonly RedisValue[] HashMembersAbsoluteExpirationSlidingExpirationData;

    /// <summary>
    /// 绝对过期时间键、滑动过期时间键
    /// </summary>
    protected static readonly RedisValue[] HashMembersAbsoluteExpirationSlidingExpiration;

    /// <summary>
    /// Redis 数据库字段
    /// </summary>
    protected static readonly FieldInfo RedisDatabaseField;

    /// <summary>
    /// 连接方法
    /// </summary>
    protected static readonly MethodInfo ConnectMethod;

    /// <summary>
    /// 异步连接方法
    /// </summary>
    protected static readonly MethodInfo ConnectAsyncMethod;

    /// <summary>
    /// 映射元数据方法
    /// </summary>
    protected static readonly MethodInfo MapMetadataMethod;

    /// <summary>
    /// 获取绝对过期时间方法
    /// </summary>
    protected static readonly MethodInfo GetAbsoluteExpirationMethod;

    /// <summary>
    /// 获取过期时间秒数方法
    /// </summary>
    protected static readonly MethodInfo GetExpirationInSecondsMethod;

    /// <summary>
    /// Redis 错误方法
    /// </summary>
    protected static readonly MethodInfo OnRedisErrorMethod;

    /// <summary>
    /// 回收方法
    /// </summary>
    protected static readonly MethodInfo RecycleMethodInfo;

    static XiHanRedisCache()
    {
        var type = typeof(RedisCache);

        RedisDatabaseField = Guard.NotNull(type.GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic), nameof(RedisDatabaseField));

        ConnectMethod = Guard.NotNull(type.GetMethod("Connect", BindingFlags.Instance | BindingFlags.NonPublic), nameof(ConnectMethod));

        ConnectAsyncMethod = Guard.NotNull(type.GetMethod("ConnectAsync", BindingFlags.Instance | BindingFlags.NonPublic), nameof(ConnectAsyncMethod));

        MapMetadataMethod = Guard.NotNull(type.GetMethod("MapMetadata", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static), nameof(MapMetadataMethod));

        GetAbsoluteExpirationMethod = Guard.NotNull(type.GetMethod("GetAbsoluteExpiration", BindingFlags.Static | BindingFlags.NonPublic), nameof(GetAbsoluteExpirationMethod));

        GetExpirationInSecondsMethod = Guard.NotNull(type.GetMethod("GetExpirationInSeconds", BindingFlags.Static | BindingFlags.NonPublic), nameof(GetExpirationInSecondsMethod));

        OnRedisErrorMethod = Guard.NotNull(type.GetMethod("OnRedisError", BindingFlags.Instance | BindingFlags.NonPublic), nameof(OnRedisErrorMethod));

        RecycleMethodInfo = Guard.NotNull(type.GetMethod("Recycle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static), nameof(RecycleMethodInfo));

        AbsoluteExpirationKey = type.GetField("AbsoluteExpirationKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!.ToString()!;

        SlidingExpirationKey = type.GetField("SlidingExpirationKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!.ToString()!;

        DataKey = type.GetField("DataKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!.ToString()!;

        NotPresent = type.GetField("NotPresent", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!.To<int>();

        HashMembersAbsoluteExpirationSlidingExpirationData = [AbsoluteExpirationKey, SlidingExpirationKey, DataKey];

        HashMembersAbsoluteExpirationSlidingExpiration = [AbsoluteExpirationKey, SlidingExpirationKey];
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="optionsAccessor"></param>
    public XiHanRedisCache(IOptions<RedisCacheOptions> optionsAccessor) : base(optionsAccessor)
    {
        var instanceName = optionsAccessor.Value.InstanceName;
        InstancePrefixString = instanceName ?? string.Empty;
        if (!string.IsNullOrEmpty(instanceName))
        {
            InstancePrefix = (RedisKey)Encoding.UTF8.GetBytes(instanceName);
        }
    }

    /// <summary>
    /// 实例前缀
    /// </summary>
    protected RedisKey InstancePrefix { get; }

    /// <summary>
    /// 实例前缀（字符串）
    /// </summary>
    protected string InstancePrefixString { get; }

    /// <summary>
    /// 获取多个
    /// </summary>
    /// <param name="keys"></param>
    /// <returns></returns>
    public virtual byte[]?[] GetMany(IEnumerable<string> keys)
    {
        keys = Guard.NotNull(keys, nameof(keys));

        return GetAndRefreshMany(keys, true);
    }

    /// <summary>
    /// 异步获取多个
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<byte[]?[]> GetManyAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        keys = Guard.NotNull(keys, nameof(keys));

        return await GetAndRefreshManyAsync(keys, true, token);
    }

    /// <summary>
    /// 设置多个
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    public virtual void SetMany(IEnumerable<KeyValuePair<string, byte[]>> items, DistributedCacheEntryOptions options)
    {
        var cache = Connect();

        try
        {
            Task.WaitAll(PipelineSetMany(cache, items, options, out var leases));
            foreach (var lease in leases)
            {
                Recycle(lease);
            }
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 异步设置多个
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task SetManyAsync(IEnumerable<KeyValuePair<string, byte[]>> items, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        var cache = await ConnectAsync(token);

        try
        {
            await Task.WhenAll(PipelineSetMany(cache, items, options, out var leases));
            foreach (var lease in leases)
            {
                Recycle(lease);
            }
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 刷新多个
    /// </summary>
    /// <param name="keys"></param>
    public virtual void RefreshMany(IEnumerable<string> keys)
    {
        keys = Guard.NotNull(keys, nameof(keys));

        GetAndRefreshMany(keys, false);
    }

    /// <summary>
    /// 异步刷新多个
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task RefreshManyAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        keys = Guard.NotNull(keys, nameof(keys));

        await GetAndRefreshManyAsync(keys, false, token);
    }

    /// <summary>
    /// 移除多个
    /// </summary>
    /// <param name="keys"></param>
    public virtual void RemoveMany(IEnumerable<string> keys)
    {
        keys = Guard.NotNull(keys, nameof(keys));

        var cache = Connect();

        try
        {
            Task.WaitAll(PipelineRemoveManyAsync(cache, keys));
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 异步移除多个
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        keys = Guard.NotNull(keys, nameof(keys));

        token.ThrowIfCancellationRequested();
        var cache = await ConnectAsync(token);

        try
        {
            await Task.WhenAll(PipelineRemoveManyAsync(cache, keys));
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 按模式获取键
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public virtual string[] GetKeys(string pattern)
    {
        var cache = Connect();
        try
        {
            var redisKeys = ScanKeys(cache, NormalizePattern(pattern));
            return [.. redisKeys.Select(ToNormalizedKey).Distinct(StringComparer.Ordinal)];
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 异步按模式获取键
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<string[]> GetKeysAsync(string pattern, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var cache = await ConnectAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var redisKeys = ScanKeys(cache, NormalizePattern(pattern));
            return [.. redisKeys.Select(ToNormalizedKey).Distinct(StringComparer.Ordinal)];
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 按模式移除键
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public virtual long RemoveByPattern(string pattern)
    {
        var cache = Connect();
        try
        {
            var redisKeys = ScanKeys(cache, NormalizePattern(pattern));
            return redisKeys.Length == 0 ? 0 : cache.KeyDelete(redisKeys);
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 异步按模式移除键
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<long> RemoveByPatternAsync(string pattern, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var cache = await ConnectAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var redisKeys = ScanKeys(cache, NormalizePattern(pattern));
            return redisKeys.Length == 0 ? 0 : await cache.KeyDeleteAsync(redisKeys);
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 执行 Lua 脚本
    /// </summary>
    /// <param name="script"></param>
    /// <param name="keys"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public virtual RedisResult ScriptEvaluate(string script, string[]? keys = null, RedisValue[]? values = null)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("Lua 脚本不能为空。", nameof(script));
        }

        var cache = Connect();

        try
        {
            var redisKeys = ToRedisKeys(keys);
            return cache.ScriptEvaluate(script, redisKeys, values);
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 异步执行 Lua 脚本
    /// </summary>
    /// <param name="script"></param>
    /// <param name="keys"></param>
    /// <param name="values"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<RedisResult> ScriptEvaluateAsync(string script, string[]? keys = null, RedisValue[]? values = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("Lua 脚本不能为空。", nameof(script));
        }

        token.ThrowIfCancellationRequested();
        var cache = await ConnectAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var redisKeys = ToRedisKeys(keys);
            return await cache.ScriptEvaluateAsync(script, redisKeys, values);
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }
    }

    /// <summary>
    /// 连接
    /// </summary>
    /// <returns></returns>
    protected virtual IDatabase Connect()
    {
        return (IDatabase)ConnectMethod.Invoke(this, [])!;
    }

    /// <summary>
    /// 异步连接
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual async ValueTask<IDatabase> ConnectAsync(CancellationToken token = default)
    {
        return await (ValueTask<IDatabase>)ConnectAsyncMethod.Invoke(this, [token])!;
    }

    /// <summary>
    /// 回收
    /// </summary>
    /// <param name="lease"></param>
    protected virtual void Recycle(byte[]? lease)
    {
        RecycleMethodInfo.Invoke(this, [lease!]);
    }

    /// <summary>
    /// 从管道移除多个
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="keys"></param>
    /// <returns></returns>
    protected virtual Task[] PipelineRemoveManyAsync(IDatabase cache, IEnumerable<string> keys)
    {
        return [.. keys.Select(key => cache.KeyDeleteAsync(InstancePrefix.Append(key)))];
    }

    /// <summary>
    /// 扫描键
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="pattern"></param>
    /// <returns></returns>
    protected virtual RedisKey[] ScanKeys(IDatabase cache, string pattern)
    {
        var keySet = new HashSet<RedisKey>();
        var prefixedPattern = string.IsNullOrEmpty(InstancePrefixString)
            ? pattern
            : InstancePrefixString + pattern;
        foreach (var endpoint in cache.Multiplexer.GetEndPoints())
        {
            IServer server;
            try
            {
                server = cache.Multiplexer.GetServer(endpoint);
            }
            catch
            {
                continue;
            }

            if (!server.IsConnected)
            {
                continue;
            }

            foreach (var key in server.Keys(database: cache.Database, pattern: prefixedPattern, pageSize: DefaultKeyScanPageSize))
            {
                keySet.Add(key);
            }
        }

        return [.. keySet];
    }

    /// <summary>
    /// 转换为 RedisKey 数组并追加实例前缀
    /// </summary>
    /// <param name="keys"></param>
    /// <returns></returns>
    protected virtual RedisKey[] ToRedisKeys(string[]? keys)
    {
        if (keys is null || keys.Length == 0)
        {
            return [];
        }

        return [.. keys.Select(key => InstancePrefix.Append(key))];
    }

    /// <summary>
    /// 获取并刷新多个
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="getData"></param>
    /// <returns></returns>
    protected virtual byte[]?[] GetAndRefreshMany(IEnumerable<string> keys, bool getData)
    {
        var cache = Connect();

        var keyArray = keys.Select(key => InstancePrefix.Append(key)).ToArray();
        byte[]?[] bytes;

        try
        {
            var results = cache.HashMemberGetMany(keyArray, GetHashFields(getData));

            Task.WaitAll(PipelineRefreshManyAndOutData(cache, keyArray, results, out bytes));
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }

        return bytes;
    }

    /// <summary>
    /// 异步获取并刷新多个
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="getData"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual async Task<byte[]?[]> GetAndRefreshManyAsync(IEnumerable<string> keys, bool getData, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        var cache = await ConnectAsync(token);

        var keyArray = keys.Select(key => InstancePrefix.Append(key)).ToArray();
        byte[]?[] bytes;

        try
        {
            var results = await cache.HashMemberGetManyAsync(keyArray, GetHashFields(getData));
            await Task.WhenAll(PipelineRefreshManyAndOutData(cache, keyArray, results, out bytes));
        }
        catch (Exception ex)
        {
            OnRedisError(ex, cache);
            throw;
        }

        return bytes;
    }

    /// <summary>
    /// 从管道刷新多个并输出数据
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="keys"></param>
    /// <param name="results"></param>
    /// <param name="bytes"></param>
    /// <returns></returns>
    protected virtual Task[] PipelineRefreshManyAndOutData(IDatabase cache, RedisKey[] keys, RedisValue[][] results, out byte[]?[] bytes)
    {
        bytes = new byte[keys.Length][];
        var tasks = new Task[keys.Length];

        for (var i = 0; i < keys.Length; i++)
        {
            if (results[i].Length >= 2)
            {
                MapMetadata(results[i], out var absExpr, out var sldExpr);

                if (sldExpr.HasValue)
                {
                    TimeSpan? expr;

                    if (absExpr.HasValue)
                    {
                        var relExpr = absExpr.Value - DateTimeOffset.UtcNow;
                        expr = relExpr <= sldExpr.Value ? relExpr : sldExpr;
                    }
                    else
                    {
                        expr = sldExpr;
                    }

                    tasks[i] = cache.KeyExpireAsync(keys[i], expr);
                }
                else
                {
                    tasks[i] = Task.CompletedTask;
                }
            }

            bytes[i] = results[i].Length >= 3 && results[i][2].HasValue ? (byte[]?)results[i][2] : null;
        }

        return tasks;
    }

    /// <summary>
    /// 从管道设置多个
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="items"></param>
    /// <param name="options"></param>
    /// <param name="leases"></param>
    /// <returns></returns>
    protected virtual Task[] PipelineSetMany(IDatabase cache, IEnumerable<KeyValuePair<string, byte[]>> items, DistributedCacheEntryOptions options, out List<byte[]?> leases)
    {
        var tasks = new List<Task>();
        leases = [];

        var createdTime = DateTimeOffset.UtcNow;

        var absoluteExpiration = GetAbsoluteExpiration(createdTime, options);

        foreach (var item in items)
        {
            var prefixedKey = InstancePrefix.Append(item.Key);
            var ttl = GetExpirationInSeconds(createdTime, absoluteExpiration, options);
            var fields = GetHashFields(Linearize(new ReadOnlySequence<byte>(item.Value), out var lease), absoluteExpiration, options.SlidingExpiration);
            leases.Add(lease);
            if (ttl is null)
            {
                tasks.Add(cache.HashSetAsync(prefixedKey, fields));
            }
            else
            {
                tasks.Add(cache.HashSetAsync(prefixedKey, fields));
                tasks.Add(cache.KeyExpireAsync(prefixedKey, TimeSpan.FromSeconds(ttl.GetValueOrDefault())));
            }
        }

        return [.. tasks];
    }

    /// <summary>
    /// 映射元数据
    /// </summary>
    /// <param name="results"></param>
    /// <param name="absoluteExpiration"></param>
    /// <param name="slidingExpiration"></param>
    protected virtual void MapMetadata(RedisValue[] results, out DateTimeOffset? absoluteExpiration, out TimeSpan? slidingExpiration)
    {
        var parameters = new object?[]
        {
            results, null, null
        };
        MapMetadataMethod.Invoke(this, parameters);

        absoluteExpiration = (DateTimeOffset?)parameters[1];
        slidingExpiration = (TimeSpan?)parameters[2];
    }

    /// <summary>
    /// 获取过期时间秒数
    /// </summary>
    /// <param name="createdTime"></param>
    /// <param name="absoluteExpiration"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected virtual long? GetExpirationInSeconds(DateTimeOffset createdTime, DateTimeOffset? absoluteExpiration, DistributedCacheEntryOptions options)
    {
        return (long?)GetExpirationInSecondsMethod.Invoke(null, [createdTime, absoluteExpiration, options]);
    }

    /// <summary>
    /// 获取绝对过期时间
    /// </summary>
    /// <param name="createdTime"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected virtual DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset createdTime, DistributedCacheEntryOptions options)
    {
        return (DateTimeOffset?)GetAbsoluteExpirationMethod.Invoke(null, [createdTime, options]);
    }

    /// <summary>
    /// Redis 错误
    /// </summary>
    /// <param name="ex"></param>
    /// <param name="cache"></param>
    protected virtual void OnRedisError(Exception ex, IDatabase cache)
    {
        OnRedisErrorMethod.Invoke(this, [ex, cache]);
    }

    /// <summary>
    /// 回收
    /// </summary>
    /// <param name="value"></param>
    /// <param name="lease"></param>
    /// <returns></returns>
    private static ReadOnlyMemory<byte> Linearize(in ReadOnlySequence<byte> value, out byte[]? lease)
    {
        // RedisValue 仅支持单段块；这几乎永远不会成为问题，但在极少数情况下，使用租用数组来协调事物
        if (value.IsSingleSegment)
        {
            lease = null;
            return value.First;
        }

        var length = checked((int)value.Length);
        lease = ArrayPool<byte>.Shared.Rent(length);
        value.CopyTo(lease);
        return new ReadOnlyMemory<byte>(lease, 0, length);
    }

    /// <summary>
    /// 获取哈希字段
    /// </summary>
    /// <param name="getData"></param>
    /// <returns></returns>
    private static RedisValue[] GetHashFields(bool getData)
    {
        return getData
            ? HashMembersAbsoluteExpirationSlidingExpirationData
            : HashMembersAbsoluteExpirationSlidingExpiration;
    }

    /// <summary>
    /// 规范化模式
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    private static string NormalizePattern(string pattern)
    {
        return string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();
    }

    /// <summary>
    /// 获取哈希字段
    /// </summary>
    /// <param name="value"></param>
    /// <param name="absoluteExpiration"></param>
    /// <param name="slidingExpiration"></param>
    /// <returns></returns>
    private static HashEntry[] GetHashFields(RedisValue value, DateTimeOffset? absoluteExpiration, TimeSpan? slidingExpiration)
    {
        return
        [
            new HashEntry(AbsoluteExpirationKey, absoluteExpiration?.Ticks ?? NotPresent),
            new HashEntry(SlidingExpirationKey, slidingExpiration?.Ticks ?? NotPresent),
            new HashEntry(DataKey, value)
        ];
    }

    /// <summary>
    /// 转换为规范化键（移除实例前缀）
    /// </summary>
    /// <param name="redisKey"></param>
    /// <returns></returns>
    private string ToNormalizedKey(RedisKey redisKey)
    {
        var key = redisKey.ToString();
        if (string.IsNullOrEmpty(InstancePrefixString))
        {
            return key;
        }

        return key.StartsWith(InstancePrefixString, StringComparison.Ordinal)
            ? key[InstancePrefixString.Length..]
            : key;
    }
}
