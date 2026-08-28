// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Concurrent;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Tests.Fakes;

/// <summary>
/// 只实现基础分布式缓存契约的内存替身
/// </summary>
/// <remarks>
/// 故意不实现 <see cref="ICacheSupportsMultipleItems"/> 等能力接口，用于驱动 DistributedCache 的逐条回退分支。
/// 内部用并发字典承载，可直接用于并发用例。
/// </remarks>
internal class FakeDistributedCacheStore : IDistributedCache
{
    private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

    private int _setCount;
    private int _getCount;

    /// <summary>
    /// 当前已写入的规范化键
    /// </summary>
    public IReadOnlyCollection<string> StoredKeys => [.. _entries.Keys];

    /// <summary>
    /// 写入次数
    /// </summary>
    public int SetCount => Volatile.Read(ref _setCount);

    /// <summary>
    /// 单键读取次数
    /// </summary>
    public int GetCount => Volatile.Read(ref _getCount);

    /// <summary>
    /// 被刷新的键，按调用先后记录
    /// </summary>
    public ConcurrentQueue<string> RefreshedKeys { get; } = new();

    /// <summary>
    /// 被移除的键，按调用先后记录
    /// </summary>
    public ConcurrentQueue<string> RemovedKeys { get; } = new();

    /// <summary>
    /// 最近一次写入所使用的缓存条目选项
    /// </summary>
    public DistributedCacheEntryOptions? LastSetOptions { get; private set; }

    /// <summary>
    /// 读取指定键
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <returns>字节内容，未命中为空</returns>
    public byte[]? Get(string key)
    {
        Interlocked.Increment(ref _getCount);

        return _entries.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// 异步读取指定键
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="token">取消令牌</param>
    /// <returns>字节内容，未命中为空</returns>
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        return Task.FromResult(Get(key));
    }

    /// <summary>
    /// 写入指定键
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="value">字节内容</param>
    /// <param name="options">缓存条目选项</param>
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        LastSetOptions = options;
        Interlocked.Increment(ref _setCount);
        _entries[key] = value;
    }

    /// <summary>
    /// 异步写入指定键
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="value">字节内容</param>
    /// <param name="options">缓存条目选项</param>
    /// <param name="token">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 刷新指定键
    /// </summary>
    /// <param name="key">规范化键</param>
    public void Refresh(string key)
    {
        RefreshedKeys.Enqueue(key);
    }

    /// <summary>
    /// 异步刷新指定键
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="token">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        Refresh(key);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 移除指定键
    /// </summary>
    /// <param name="key">规范化键</param>
    public void Remove(string key)
    {
        RemovedKeys.Enqueue(key);
        _entries.TryRemove(key, out _);
    }

    /// <summary>
    /// 异步移除指定键
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="token">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);

        return Task.CompletedTask;
    }
}

/// <summary>
/// 实现全部能力接口的内存替身
/// </summary>
/// <remarks>
/// 覆盖批量、键模式与脚本三类能力，用于驱动 DistributedCache 的能力分支并记录透传给底层的参数。
/// </remarks>
internal sealed class FakeCapableDistributedCacheStore : FakeDistributedCacheStore,
    ICacheSupportsMultipleItems,
    ICacheSupportsKeyPattern,
    ICacheSupportsLuaScript
{
    /// <summary>
    /// 批量读取次数
    /// </summary>
    public int GetManyCount { get; private set; }

    /// <summary>
    /// 批量写入次数
    /// </summary>
    public int SetManyCount { get; private set; }

    /// <summary>
    /// 批量刷新次数
    /// </summary>
    public int RefreshManyCount { get; private set; }

    /// <summary>
    /// 批量移除次数
    /// </summary>
    public int RemoveManyCount { get; private set; }

    /// <summary>
    /// 最近一次批量读取收到的规范化键
    /// </summary>
    public string[] LastGetManyKeys { get; private set; } = [];

    /// <summary>
    /// 按模式查询时返回的规范化键，由用例预置
    /// </summary>
    public string[] PatternKeys { get; set; } = [];

    /// <summary>
    /// 最近一次收到的规范化模式
    /// </summary>
    public string? LastPattern { get; private set; }

    /// <summary>
    /// 按模式移除时返回的数量，由用例预置
    /// </summary>
    public long RemoveByPatternResult { get; set; }

    /// <summary>
    /// 按模式移除的调用次数
    /// </summary>
    public int RemoveByPatternCount { get; private set; }

    /// <summary>
    /// 最近一次收到的脚本
    /// </summary>
    public string? LastScript { get; private set; }

    /// <summary>
    /// 最近一次收到的规范化脚本键
    /// </summary>
    public string[]? LastScriptKeys { get; private set; }

    /// <summary>
    /// 最近一次收到的脚本参数
    /// </summary>
    public object?[]? LastScriptValues { get; private set; }

    /// <summary>
    /// 脚本执行返回的结果，由用例预置
    /// </summary>
    public CacheScriptResult ScriptResult { get; set; } = CacheScriptResult.FromValue(1L);

    /// <summary>
    /// 批量读取
    /// </summary>
    /// <param name="keys">规范化键集合</param>
    /// <returns>与键顺序一一对应的字节内容</returns>
    public byte[]?[] GetMany(IEnumerable<string> keys)
    {
        var keyArray = keys.ToArray();
        GetManyCount++;
        LastGetManyKeys = keyArray;

        var values = new List<byte[]?>();
        foreach (var key in keyArray)
        {
            values.Add(Get(key));
        }

        return [.. values];
    }

    /// <summary>
    /// 异步批量读取
    /// </summary>
    /// <param name="keys">规范化键集合</param>
    /// <param name="token">取消令牌</param>
    /// <returns>与键顺序一一对应的字节内容</returns>
    public Task<byte[]?[]> GetManyAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        return Task.FromResult(GetMany(keys));
    }

    /// <summary>
    /// 批量写入
    /// </summary>
    /// <param name="items">键值对集合</param>
    /// <param name="options">缓存条目选项</param>
    public void SetMany(IEnumerable<KeyValuePair<string, byte[]>> items, DistributedCacheEntryOptions options)
    {
        SetManyCount++;
        foreach (var item in items)
        {
            Set(item.Key, item.Value, options);
        }
    }

    /// <summary>
    /// 异步批量写入
    /// </summary>
    /// <param name="items">键值对集合</param>
    /// <param name="options">缓存条目选项</param>
    /// <param name="token">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task SetManyAsync(IEnumerable<KeyValuePair<string, byte[]>> items, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        SetMany(items, options);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量刷新
    /// </summary>
    /// <param name="keys">规范化键集合</param>
    public void RefreshMany(IEnumerable<string> keys)
    {
        RefreshManyCount++;
        foreach (var key in keys)
        {
            Refresh(key);
        }
    }

    /// <summary>
    /// 异步批量刷新
    /// </summary>
    /// <param name="keys">规范化键集合</param>
    /// <param name="token">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task RefreshManyAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        RefreshMany(keys);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量移除
    /// </summary>
    /// <param name="keys">规范化键集合</param>
    public void RemoveMany(IEnumerable<string> keys)
    {
        RemoveManyCount++;
        foreach (var key in keys)
        {
            Remove(key);
        }
    }

    /// <summary>
    /// 异步批量移除
    /// </summary>
    /// <param name="keys">规范化键集合</param>
    /// <param name="token">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken token = default)
    {
        RemoveMany(keys);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 按模式查询键
    /// </summary>
    /// <param name="pattern">规范化模式</param>
    /// <returns>预置的规范化键集合</returns>
    public string[] GetKeys(string pattern)
    {
        LastPattern = pattern;

        return PatternKeys;
    }

    /// <summary>
    /// 异步按模式查询键
    /// </summary>
    /// <param name="pattern">规范化模式</param>
    /// <param name="token">取消令牌</param>
    /// <returns>预置的规范化键集合</returns>
    public Task<string[]> GetKeysAsync(string pattern, CancellationToken token = default)
    {
        return Task.FromResult(GetKeys(pattern));
    }

    /// <summary>
    /// 按模式移除键
    /// </summary>
    /// <param name="pattern">规范化模式</param>
    /// <returns>预置的移除数量</returns>
    public long RemoveByPattern(string pattern)
    {
        LastPattern = pattern;
        RemoveByPatternCount++;

        return RemoveByPatternResult;
    }

    /// <summary>
    /// 异步按模式移除键
    /// </summary>
    /// <param name="pattern">规范化模式</param>
    /// <param name="token">取消令牌</param>
    /// <returns>预置的移除数量</returns>
    public Task<long> RemoveByPatternAsync(string pattern, CancellationToken token = default)
    {
        return Task.FromResult(RemoveByPattern(pattern));
    }

    /// <summary>
    /// 执行脚本
    /// </summary>
    /// <param name="script">脚本内容</param>
    /// <param name="keys">规范化键集合</param>
    /// <param name="values">参数集合</param>
    /// <returns>预置的脚本结果</returns>
    public CacheScriptResult ScriptEvaluate(string script, string[]? keys = null, object?[]? values = null)
    {
        LastScript = script;
        LastScriptKeys = keys;
        LastScriptValues = values;

        return ScriptResult;
    }

    /// <summary>
    /// 异步执行脚本
    /// </summary>
    /// <param name="script">脚本内容</param>
    /// <param name="keys">规范化键集合</param>
    /// <param name="values">参数集合</param>
    /// <param name="token">取消令牌</param>
    /// <returns>预置的脚本结果</returns>
    public Task<CacheScriptResult> ScriptEvaluateAsync(string script, string[]? keys = null, object?[]? values = null, CancellationToken token = default)
    {
        return Task.FromResult(ScriptEvaluate(script, keys, values));
    }
}

/// <summary>
/// 所有操作都抛错的分布式缓存替身
/// </summary>
/// <remarks>
/// 用于验证 hideErrors 的吞异常与抛异常两条分支。
/// </remarks>
internal sealed class FailingDistributedCacheStore : IDistributedCache
{
    /// <summary>
    /// 抛出的异常消息
    /// </summary>
    public const string FailureMessage = "缓存后端不可用";

    /// <summary>
    /// 读取，始终抛错
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <returns>不会返回</returns>
    public byte[]? Get(string key)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 异步读取，始终抛错
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="token">取消令牌</param>
    /// <returns>不会返回</returns>
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 写入，始终抛错
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="value">字节内容</param>
    /// <param name="options">缓存条目选项</param>
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 异步写入，始终抛错
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="value">字节内容</param>
    /// <param name="options">缓存条目选项</param>
    /// <param name="token">取消令牌</param>
    /// <returns>不会返回</returns>
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 刷新，始终抛错
    /// </summary>
    /// <param name="key">规范化键</param>
    public void Refresh(string key)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 异步刷新，始终抛错
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="token">取消令牌</param>
    /// <returns>不会返回</returns>
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 移除，始终抛错
    /// </summary>
    /// <param name="key">规范化键</param>
    public void Remove(string key)
    {
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 异步移除，始终抛错
    /// </summary>
    /// <param name="key">规范化键</param>
    /// <param name="token">取消令牌</param>
    /// <returns>不会返回</returns>
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        throw new InvalidOperationException(FailureMessage);
    }
}
