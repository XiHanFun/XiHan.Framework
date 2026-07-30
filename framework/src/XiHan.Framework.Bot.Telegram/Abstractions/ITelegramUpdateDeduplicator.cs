// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Abstractions;

/// <summary>
/// Telegram Update 幂等去重器（拦截 Webhook 重发 / 轮询重复投递）
/// </summary>
/// <remarks>
/// 缓存键使用机器人名称而非 Token（避免 Token 泄漏面）。
/// 默认实现为进程内 TTL 字典（多实例部署时无效）；应用层可注册分布式缓存实现覆盖（TryAdd 语义）。
/// </remarks>
public interface ITelegramUpdateDeduplicator
{
    /// <summary>
    /// 尝试将指定 Update 标记为已处理
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="updateId">Update Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>true 表示首次处理（已占位成功）；false 表示重复投递（应跳过）</returns>
    Task<bool> TryMarkProcessedAsync(string botName, int updateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚指定 Update 的幂等标记（处理被取消时调用，保证 at-least-once：允许重发/重投后重新处理）
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="updateId">Update Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task TryUnmarkAsync(string botName, int updateId, CancellationToken cancellationToken = default);
}
