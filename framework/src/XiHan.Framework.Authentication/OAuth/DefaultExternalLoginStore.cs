#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultExternalLoginStore
// Guid:a1b2c3d4-5e6f-7890-abcd-ef1234567805
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 默认内存实现（仅供开发/测试，生产环境请实现数据库持久化）
/// </summary>
public class DefaultExternalLoginStore : IExternalLoginStore
{
    private readonly ConcurrentDictionary<string, long> _store = new();

    /// <inheritdoc/>
    public Task<long?> FindUserIdAsync(string provider, string providerKey, long? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(provider, providerKey, tenantId);
        return Task.FromResult(_store.TryGetValue(key, out var userId) ? (long?)userId : null);
    }

    /// <inheritdoc/>
    public Task CreateAsync(long userId, ExternalLoginInfo info, long? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(info.Provider, info.ProviderKey, tenantId);
        _store[key] = userId;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
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
