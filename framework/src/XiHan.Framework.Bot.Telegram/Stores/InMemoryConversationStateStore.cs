// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Bot.Telegram.Abstractions;

namespace XiHan.Framework.Bot.Telegram.Stores;

/// <summary>
/// 进程内会话状态存储（TTL 字典；多实例部署请以分布式实现覆盖）
/// </summary>
public class InMemoryConversationStateStore : IConversationStateStore
{
    private readonly ConcurrentDictionary<string, StateEntry> _states = new(StringComparer.Ordinal);

    /// <summary>
    /// 获取指定会话的当前状态，已过期的条目会被移除并按无状态返回
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话状态；null 表示无活跃状态</returns>
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

    /// <summary>
    /// 设置指定会话的状态（覆盖已有状态），存活时长非正数时取 10 分钟
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="state">会话状态</param>
    /// <param name="ttl">存活时长</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task SetAsync(string botName, long chatId, long userId, ConversationState state, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var key = BuildKey(botName, chatId, userId);
        var effectiveTtl = ttl > TimeSpan.Zero ? ttl : TimeSpan.FromMinutes(10);
        _states[key] = new StateEntry(state, DateTimeOffset.UtcNow.Add(effectiveTtl));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 清除指定会话的状态
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
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
