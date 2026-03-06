#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDistributedCache
// Guid:7d092659-7538-4e99-ae16-c38ab6dd15ca
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 05:25:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 用于缓存 <typeparamref name="TCacheItem" /> 类型对象的分布式缓存
/// </summary>
/// <typeparam name="TCacheItem">缓存项的类型</typeparam>
public interface IDistributedCache<TCacheItem> : IDistributedCache<TCacheItem, string> where TCacheItem : class
{
    /// <summary>
    /// 获取内部缓存
    /// </summary>
    IDistributedCache<TCacheItem, string> InternalCache { get; }
}

/// <summary>
/// 用于缓存 <typeparamref name="TCacheItem" /> 类型对象的分布式缓存
/// 使用 <typeparamref name="TCacheKey" /> 类型作为缓存键的泛型类型
/// </summary>
/// <typeparam name="TCacheItem">缓存项的类型</typeparam>
/// <typeparam name="TCacheKey">缓存键的类型</typeparam>
public interface IDistributedCache<TCacheItem, TCacheKey> where TCacheItem : class
{
    /// <summary>
    /// 根据指定的键获取缓存项如果找不到缓存项，则返回 null
    /// </summary>
    /// <param name="key">要从缓存中检索的缓存项的键</param>
    /// <param name="hideErrors">是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">是否将缓存存储在当前工作单元中，直到工作单元结束</param>
    /// <returns>缓存项或 null</returns>
    TCacheItem? Get(TCacheKey key, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 根据指定的多个键获取缓存项
    /// 返回的列表与提供的键数量相同
    /// 列表中的某个项不能为 null，但如果相关键未找到，则项的值为 null
    /// </summary>
    /// <param name="keys">要从缓存中检索的缓存项的键集合</param>
    /// <param name="hideErrors">是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">是否将缓存存储在当前工作单元中，直到工作单元结束</param>
    /// <returns>缓存项的列表</returns>
    KeyValuePair<TCacheKey, TCacheItem?>[] GetMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 异步获取多个键对应的缓存项
    /// 返回的列表与提供的键数量相同
    /// 列表中的某个项不能为 null，但如果相关键未找到，则项的值为 null
    /// </summary>
    /// <param name="keys">要从缓存中检索的缓存项的键集合</param>
    /// <param name="hideErrors">是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">是否将缓存存储在当前工作单元中，直到工作单元结束</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>缓存项的列表</returns>
    Task<KeyValuePair<TCacheKey, TCacheItem?>[]> GetManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 根据指定的键获取缓存项如果找不到缓存项，则返回 null
    /// </summary>
    /// <param name="key">要从缓存中检索的缓存项的键</param>
    /// <param name="hideErrors">是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">是否将缓存存储在当前工作单元中，直到工作单元结束</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>缓存项或 null</returns>
    Task<TCacheItem?> GetAsync(TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 获取或添加指定键的缓存项如果找不到缓存项，则使用 <paramref name="factory" /> 委托提供的缓存项，并返回它
    /// </summary>
    /// <param name="key">要从缓存中检索的缓存项的键</param>
    /// <param name="factory">当找不到缓存项时，使用该委托提供缓存项</param>
    /// <param name="optionsFactory">缓存选项的工厂委托</param>
    /// <param name="hideErrors">是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">是否将缓存存储在当前工作单元中，直到工作单元结束</param>
    /// <returns>缓存项</returns>
    TCacheItem? GetOrAdd(TCacheKey key, Func<TCacheItem> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 异步获取或添加指定键的缓存项如果找不到缓存项，则使用 <paramref name="factory" /> 委托提供的缓存项，并返回它
    /// </summary>
    /// <param name="key">要从缓存中检索的缓存项的键</param>
    /// <param name="factory">当找不到缓存项时，使用该委托提供缓存项</param>
    /// <param name="optionsFactory">缓存选项的工厂委托</param>
    /// <param name="hideErrors">是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">是否将缓存存储在当前工作单元中，直到工作单元结束</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>缓存项</returns>
    Task<TCacheItem?> GetOrAddAsync(TCacheKey key, Func<Task<TCacheItem>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 获取或添加多个缓存项，使用给定的键，如果某些缓存项未找到，则使用 <paramref name="factory" /> 委托添加缓存项，并返回提供的缓存项
    /// </summary>
    /// <param name="keys">要从缓存中检索的缓存项的键</param>
    /// <param name="factory">当未找到指定 <paramref name="keys" /> 的缓存项时，用于提供缓存项的工厂委托</param>
    /// <param name="optionsFactory">工厂委托的缓存选项</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">此选项将缓存存储在当前工作单元中，直到当前工作单元结束才会影响缓存</param>
    /// <returns>缓存项</returns>
    KeyValuePair<TCacheKey, TCacheItem?>[] GetOrAddMany(IEnumerable<TCacheKey> keys, Func<IEnumerable<TCacheKey>, List<KeyValuePair<TCacheKey, TCacheItem>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 获取或添加多个缓存项，使用给定的键，如果某些缓存项未找到，则使用 <paramref name="factory" /> 委托添加缓存项，并返回提供的缓存项
    /// </summary>
    /// <param name="keys">要从缓存中检索的缓存项的键</param>
    /// <param name="factory">当未找到指定 <paramref name="keys" /> 的缓存项时，用于提供缓存项的工厂委托</param>
    /// <param name="optionsFactory">工厂委托的缓存选项</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">此选项将缓存存储在当前工作单元中，直到当前工作单元结束才会影响缓存</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>缓存项</returns>
    Task<KeyValuePair<TCacheKey, TCacheItem?>[]> GetOrAddManyAsync(IEnumerable<TCacheKey> keys, Func<IEnumerable<TCacheKey>, Task<List<KeyValuePair<TCacheKey, TCacheItem>>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 设置指定键的缓存项值
    /// </summary>
    /// <param name="key">要从缓存中检索的缓存项的键</param>
    /// <param name="value">要在缓存中设置的缓存项值</param>
    /// <param name="options">缓存值的缓存选项</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">此选项将缓存存储在当前工作单元中，直到当前工作单元结束才会影响缓存</param>
    void Set(TCacheKey key, TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 设置指定键的缓存项值
    /// </summary>
    /// <param name="key">要从缓存中检索的缓存项的键</param>
    /// <param name="value">要在缓存中设置的缓存项值</param>
    /// <param name="options">缓存值的缓存选项</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">此选项将缓存存储在当前工作单元中，直到当前工作单元结束才会影响缓存</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>一个异步任务，表示操作的完成</returns>
    Task SetAsync(TCacheKey key, TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 设置多个缓存项
    /// 根据实现方式，这可能比单独设置多个项更高效
    /// </summary>
    /// <param name="items">要在缓存中设置的项</param>
    /// <param name="options">缓存值的缓存选项</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">此选项将缓存存储在当前工作单元中，直到当前工作单元结束才会影响缓存</param>
    void SetMany(IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 设置多个缓存项
    /// 根据实现方式，这可能比单独设置多个项更高效
    /// </summary>
    /// <param name="items">要在缓存中设置的项</param>
    /// <param name="options">缓存值的缓存选项</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">此选项将缓存存储在当前工作单元中，直到当前工作单元结束才会影响缓存</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>一个异步任务，表示操作的完成</returns>
    Task SetManyAsync(IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 刷新指定键的缓存值，并重置其滑动过期时间
    /// </summary>
    /// <param name="key">要刷新缓存值的键</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    void Refresh(TCacheKey key, bool? hideErrors = null);

    /// <summary>
    /// 刷新指定键的缓存值，并重置其滑动过期时间
    /// </summary>
    /// <param name="key">要刷新缓存值的键</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>一个异步任务，表示操作的完成</returns>
    Task RefreshAsync(TCacheKey key, bool? hideErrors = null, CancellationToken token = default);

    /// <summary>
    /// 刷新多个键的缓存值，并重置其滑动过期时间
    /// 此实现可以比单独刷新多个项更高效
    /// </summary>
    /// <param name="keys">要刷新缓存值的键集合</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    void RefreshMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null);

    /// <summary>
    /// 刷新多个键的缓存值，并重置其滑动过期时间
    /// 此实现可以比单独刷新多个项更高效
    /// </summary>
    /// <param name="keys">要刷新缓存值的键集合</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>一个异步任务，表示操作的完成</returns>
    Task RefreshManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, CancellationToken token = default);

    /// <summary>
    /// 从缓存中移除指定键的缓存项
    /// </summary>
    /// <param name="key">要移除的缓存项的键</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">指示是否将缓存存储在当前工作单元中，直到当前工作单元结束才移除</param>
    void Remove(TCacheKey key, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 从缓存中移除指定键的缓存项
    /// </summary>
    /// <param name="key">要移除的缓存项的键</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">指示是否将缓存存储在当前工作单元中，直到当前工作单元结束才移除</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>一个异步任务，表示操作的完成</returns>
    Task RemoveAsync(TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 从缓存中移除多个键的缓存项
    /// </summary>
    /// <param name="keys">要移除的缓存项的键集合</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">指示是否将缓存存储在当前工作单元中，直到当前工作单元结束才移除</param>
    void RemoveMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 从缓存中移除多个键的缓存项
    /// </summary>
    /// <param name="keys">要移除的缓存项的键集合</param>
    /// <param name="hideErrors">指示是否隐藏分布式缓存的异常</param>
    /// <param name="considerUow">指示是否将缓存存储在当前工作单元中，直到当前工作单元结束才移除</param>
    /// <param name="token">任务的 <see cref="T:System.Threading.CancellationToken" /></param>
    /// <returns>一个异步任务，表示操作的完成</returns>
    Task RemoveManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 判断指定键是否存在
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="hideErrors">是否隐藏分布式缓存异常</param>
    /// <param name="considerUow">是否考虑当前工作单元中的缓存变更</param>
    /// <returns>存在返回 true；否则返回 false</returns>
    bool Exists(TCacheKey key, bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 异步判断指定键是否存在
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="hideErrors">是否隐藏分布式缓存异常</param>
    /// <param name="considerUow">是否考虑当前工作单元中的缓存变更</param>
    /// <param name="token">取消令牌</param>
    /// <returns>存在返回 true；否则返回 false</returns>
    Task<bool> ExistsAsync(TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 按模式获取当前缓存下的所有键（仅支持字符串键缓存）
    /// </summary>
    /// <param name="pattern">业务键匹配模式，默认 "*" </param>
    /// <param name="hideErrors">是否隐藏分布式缓存异常</param>
    /// <param name="considerUow">是否考虑当前工作单元中的缓存变更</param>
    /// <returns>匹配到的键集合</returns>
    TCacheKey[] GetKeys(string pattern = "*", bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 异步按模式获取当前缓存下的所有键（仅支持字符串键缓存）
    /// </summary>
    /// <param name="pattern">业务键匹配模式，默认 "*" </param>
    /// <param name="hideErrors">是否隐藏分布式缓存异常</param>
    /// <param name="considerUow">是否考虑当前工作单元中的缓存变更</param>
    /// <param name="token">取消令牌</param>
    /// <returns>匹配到的键集合</returns>
    Task<TCacheKey[]> GetKeysAsync(string pattern = "*", bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 按模式移除缓存项（仅支持字符串键缓存）
    /// </summary>
    /// <param name="pattern">业务键匹配模式，默认 "*" </param>
    /// <param name="hideErrors">是否隐藏分布式缓存异常</param>
    /// <param name="considerUow">是否考虑当前工作单元中的缓存变更</param>
    /// <returns>实际移除的键数量</returns>
    long RemoveByPattern(string pattern = "*", bool? hideErrors = null, bool considerUow = false);

    /// <summary>
    /// 异步按模式移除缓存项（仅支持字符串键缓存）
    /// </summary>
    /// <param name="pattern">业务键匹配模式，默认 "*" </param>
    /// <param name="hideErrors">是否隐藏分布式缓存异常</param>
    /// <param name="considerUow">是否考虑当前工作单元中的缓存变更</param>
    /// <param name="token">取消令牌</param>
    /// <returns>实际移除的键数量</returns>
    Task<long> RemoveByPatternAsync(string pattern = "*", bool? hideErrors = null, bool considerUow = false, CancellationToken token = default);

    /// <summary>
    /// 执行 Lua 脚本
    /// </summary>
    /// <param name="script">Lua 脚本</param>
    /// <param name="keys">脚本键集合（业务键）</param>
    /// <param name="values">脚本参数集合</param>
    /// <param name="hideErrors">是否隐藏分布式缓存异常</param>
    /// <returns>脚本执行结果</returns>
    RedisResult? ScriptEvaluate(string script, IEnumerable<TCacheKey>? keys = null, IEnumerable<RedisValue>? values = null, bool? hideErrors = null);

    /// <summary>
    /// 异步执行 Lua 脚本
    /// </summary>
    /// <param name="script">Lua 脚本</param>
    /// <param name="keys">脚本键集合（业务键）</param>
    /// <param name="values">脚本参数集合</param>
    /// <param name="hideErrors">是否隐藏分布式缓存异常</param>
    /// <param name="token">取消令牌</param>
    /// <returns>脚本执行结果</returns>
    Task<RedisResult?> ScriptEvaluateAsync(string script, IEnumerable<TCacheKey>? keys = null, IEnumerable<RedisValue>? values = null, bool? hideErrors = null, CancellationToken token = default);
}
