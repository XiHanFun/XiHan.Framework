// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Abstractions;

/// <summary>
/// Telegram 出站消息审计存储
/// </summary>
/// <remarks>
/// 默认实现为 no-op；应用层可注册数据库/队列实现覆盖（TryAdd 语义）。
/// 审计失败不影响消息发送主流程（调用方吞异常仅记日志）。
/// </remarks>
public interface ITelegramMessageAuditStore
{
    /// <summary>
    /// 追加一条出站消息审计记录
    /// </summary>
    /// <param name="record">审计记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AppendAsync(TelegramMessageAuditRecord record, CancellationToken cancellationToken = default);
}
