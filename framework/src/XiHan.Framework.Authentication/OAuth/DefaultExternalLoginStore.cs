// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 默认内存实现（仅供开发/测试，生产环境请实现数据库持久化）
/// </summary>
public class DefaultExternalLoginStore : IExternalLoginStore
{
    private readonly ConcurrentDictionary<string, long> _store = new();

    /// <summary>
    /// 从内存字典中按提供商和提供商用户标识查找关联的内部用户标识
    /// </summary>
    /// <param name="provider">提供商名称</param>
    /// <param name="providerKey">提供商用户标识</param>
    /// <param name="tenantId">租户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>内部用户标识，未绑定返回 null</returns>
    public Task<long?> FindUserIdAsync(string provider, string providerKey, long? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(provider, providerKey, tenantId);
        return Task.FromResult(_store.TryGetValue(key, out var userId) ? (long?)userId : null);
    }

    /// <summary>
    /// 在内存字典中写入第三方登录绑定记录，已存在同键时覆盖
    /// </summary>
    /// <param name="userId">内部用户标识</param>
    /// <param name="info">第三方登录信息</param>
    /// <param name="tenantId">租户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task CreateAsync(long userId, ExternalLoginInfo info, long? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(info.Provider, info.ProviderKey, tenantId);
        _store[key] = userId;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 从内存字典中删除该用户在指定提供商下的全部绑定记录
    /// </summary>
    /// <param name="userId">内部用户标识</param>
    /// <param name="provider">提供商名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task RemoveAsync(long userId, string provider, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _store.Where(kv => kv.Value == userId && kv.Key.StartsWith($"{provider}:", StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    private static string BuildKey(string provider, string providerKey, long? tenantId)
    {
        return $"{provider}:{providerKey}:{tenantId ?? 0}";
    }
}
