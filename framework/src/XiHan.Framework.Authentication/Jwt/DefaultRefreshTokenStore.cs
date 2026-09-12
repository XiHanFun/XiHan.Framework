// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace XiHan.Framework.Authentication.Jwt;

/// <summary>
/// 默认刷新令牌存储（有界进程内实现）
/// </summary>
public class DefaultRefreshTokenStore : IRefreshTokenStore
{
    private const int CleanupFrequency = 256;
    private const int MaxEntryCount = 100000;

    private readonly ConcurrentDictionary<string, RefreshTokenEntry> _tokens = new(StringComparer.Ordinal);
    private readonly Lock _syncRoot = new();

    private int _saveCount;

    /// <summary>
    /// 保存刷新令牌
    /// </summary>
    public void Save(string refreshToken, string? subject, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (expiresAt <= now)
        {
            _tokens.TryRemove(refreshToken, out _);
            return;
        }

        lock (_syncRoot)
        {
            _saveCount++;
            if (_saveCount % CleanupFrequency == 0 || _tokens.Count >= MaxEntryCount)
            {
                CleanupExpired(now);
            }

            if (!_tokens.ContainsKey(refreshToken) && _tokens.Count >= MaxEntryCount)
            {
                throw new InvalidOperationException($"默认刷新令牌存储已达到 {MaxEntryCount} 条上限，请替换为应用级持久化实现。");
            }

            _tokens[refreshToken] = new RefreshTokenEntry(subject, expiresAt);
        }
    }

    /// <summary>
    /// 校验刷新令牌
    /// </summary>
    public bool Validate(string refreshToken, string? subject = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        if (!_tokens.TryGetValue(refreshToken, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            _tokens.TryRemove(refreshToken, out _);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(subject) &&
            !string.Equals(entry.Subject, subject, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 移除刷新令牌
    /// </summary>
    public void Remove(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        _tokens.TryRemove(refreshToken, out _);
    }

    private void CleanupExpired(DateTime now)
    {
        foreach (var entry in _tokens)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                _tokens.TryRemove(entry.Key, out _);
            }
        }
    }

    private sealed record RefreshTokenEntry(string? Subject, DateTime ExpiresAt);
}
