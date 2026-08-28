// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Fakes;

/// <summary>
/// 平台全局设置存储手写替身
/// </summary>
/// <remarks>
/// 管理器与分发器都按「每次都重新问存储」的契约实时读取设置，
/// 这里把设置暴露成可写属性，便于在两次读取之间模拟应用层热更新。
/// </remarks>
internal sealed class FakeTelegramBotSettingsStore : ITelegramBotSettingsStore
{
    /// <summary>
    /// 当前生效设置
    /// </summary>
    public TelegramBotSettings Settings { get; set; } = new();

    /// <summary>
    /// 设置后读取时抛出该异常
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>
    /// 被读取的次数
    /// </summary>
    public int GetCount { get; private set; }

    /// <summary>
    /// 获取当前生效的平台全局设置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>平台全局设置</returns>
    public Task<TelegramBotSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        GetCount++;
        return ExceptionToThrow is null
            ? Task.FromResult(Settings)
            : Task.FromException<TelegramBotSettings>(ExceptionToThrow);
    }
}

/// <summary>
/// 机器人配置列表存储手写替身
/// </summary>
internal sealed class FakeTelegramBotConfigStore : ITelegramBotConfigStore
{
    /// <summary>
    /// 当前生效的机器人配置列表
    /// </summary>
    public List<TelegramBotConfig> Configs { get; set; } = [];

    /// <summary>
    /// 被读取的次数
    /// </summary>
    public int GetCount { get; private set; }

    /// <summary>
    /// 获取当前生效的机器人配置列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>机器人配置列表</returns>
    public Task<IReadOnlyList<TelegramBotConfig>> GetBotConfigsAsync(CancellationToken cancellationToken = default)
    {
        GetCount++;
        IReadOnlyList<TelegramBotConfig> configs = [.. Configs];
        return Task.FromResult(configs);
    }
}

/// <summary>
/// 单发通道配置存储手写替身
/// </summary>
internal sealed class FakeTelegramConfigStore : ITelegramConfigStore
{
    /// <summary>
    /// 构造未配置的替身
    /// </summary>
    public FakeTelegramConfigStore()
    {
    }

    /// <summary>
    /// 构造返回指定配置的替身
    /// </summary>
    /// <param name="options">当前生效配置</param>
    public FakeTelegramConfigStore(TelegramOptions? options)
    {
        Options = options;
    }

    /// <summary>
    /// 当前生效配置
    /// </summary>
    public TelegramOptions? Options { get; set; }

    /// <summary>
    /// 被读取的次数
    /// </summary>
    public int GetCount { get; private set; }

    /// <summary>
    /// 最后一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前生效配置</returns>
    public Task<TelegramOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        GetCount++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Options);
    }
}

/// <summary>
/// Update 幂等去重器手写替身
/// </summary>
/// <remarks>
/// 分发器的幂等契约有两条：命中重复直接短路、处理被取消时必须回滚标记且回滚不能带已取消的令牌。
/// 这里同时记录标记与回滚的调用，用来精确断言这两条。
/// </remarks>
internal sealed class FakeTelegramUpdateDeduplicator : ITelegramUpdateDeduplicator
{
    /// <summary>
    /// 标记结果（false 表示模拟命中重复投递）
    /// </summary>
    public bool MarkResult { get; set; } = true;

    /// <summary>
    /// 标记调用记录
    /// </summary>
    public List<(string BotName, int UpdateId)> Marked { get; } = [];

    /// <summary>
    /// 回滚调用记录
    /// </summary>
    public List<(string BotName, int UpdateId)> Unmarked { get; } = [];

    /// <summary>
    /// 最后一次回滚时收到的取消令牌
    /// </summary>
    public CancellationToken LastUnmarkCancellationToken { get; private set; } = new(canceled: true);

    /// <summary>
    /// 尝试将指定 Update 标记为已处理
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="updateId">Update Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否首次处理</returns>
    public Task<bool> TryMarkProcessedAsync(string botName, int updateId, CancellationToken cancellationToken = default)
    {
        Marked.Add((botName, updateId));
        return Task.FromResult(MarkResult);
    }

    /// <summary>
    /// 回滚指定 Update 的幂等标记
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="updateId">Update Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task TryUnmarkAsync(string botName, int updateId, CancellationToken cancellationToken = default)
    {
        Unmarked.Add((botName, updateId));
        LastUnmarkCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 会话状态存储手写替身
/// </summary>
internal sealed class FakeConversationStateStore : IConversationStateStore
{
    /// <summary>
    /// 当前活跃状态（null 表示无状态）
    /// </summary>
    public ConversationState? State { get; set; }

    /// <summary>
    /// 被读取的次数
    /// </summary>
    public int GetCount { get; private set; }

    /// <summary>
    /// 被清除的次数
    /// </summary>
    public int RemoveCount { get; private set; }

    /// <summary>
    /// 获取指定会话的当前状态
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话状态</returns>
    public Task<ConversationState?> GetAsync(string botName, long chatId, long userId, CancellationToken cancellationToken = default)
    {
        GetCount++;
        return Task.FromResult(State);
    }

    /// <summary>
    /// 设置指定会话的状态
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="state">会话状态</param>
    /// <param name="ttl">存活时长</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task SetAsync(string botName, long chatId, long userId, ConversationState state, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        State = state;
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
        RemoveCount++;
        State = null;
        return Task.CompletedTask;
    }
}
