#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryRefreshTokenStore
// Guid:95e6f7a1-55f7-4f39-a8f5-2e6b7f8c9d31
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 14:12:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Authentication.Jwt;

/// <summary>
/// 基于内存的刷新令牌存储
/// </summary>
public class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenEntry> _tokens = new(StringComparer.Ordinal);

    /// <summary>
    /// 保存刷新令牌
    /// </summary>
    public void Save(string refreshToken, string? subject, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        _tokens[refreshToken] = new RefreshTokenEntry(subject, expiresAt);
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

    private sealed record RefreshTokenEntry(string? Subject, DateTime ExpiresAt);
}
