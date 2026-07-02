#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryConversationStateStore
// Guid:e4dbcad5-477c-47eb-97ed-9748da92dbdd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Bot.Telegram.Platform.Abstractions;

namespace XiHan.Framework.Bot.Telegram.Platform.Stores;

/// <summary>
/// 进程内会话状态存储（TTL 字典；多实例部署请以分布式实现覆盖）
/// </summary>
public class InMemoryConversationStateStore : IConversationStateStore
{
    private readonly ConcurrentDictionary<string, StateEntry> _states = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<ConversationState?> GetAsync(string botName, long chatId, long userId, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(botName, chatId, userId);
        if (!_states.TryGetValue(key, out var entry))
        {
            return Task.FromResult<ConversationState?>(null);
        }

        if (entry.ExpirationTime <= DateTimeOffset.UtcNow)
        {
            _ = _states.TryRemove(key, out _);
            return Task.FromResult<ConversationState?>(null);
        }

        return Task.FromResult<ConversationState?>(entry.State);
    }

    /// <inheritdoc />
    public Task SetAsync(string botName, long chatId, long userId, ConversationState state, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var key = BuildKey(botName, chatId, userId);
        var effectiveTtl = ttl > TimeSpan.Zero ? ttl : TimeSpan.FromMinutes(10);
        _states[key] = new StateEntry(state, DateTimeOffset.UtcNow.Add(effectiveTtl));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string botName, long chatId, long userId, CancellationToken cancellationToken = default)
    {
        _ = _states.TryRemove(BuildKey(botName, chatId, userId), out _);
        return Task.CompletedTask;
    }

    private static string BuildKey(string botName, long chatId, long userId)
    {
        return $"{botName}:{chatId}:{userId}";
    }

    private sealed record StateEntry(ConversationState State, DateTimeOffset ExpirationTime);
}
