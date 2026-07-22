// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Abstractions;

namespace XiHan.Framework.Bot.Telegram.Stores;

/// <summary>
/// 空操作出站消息审计存储（默认实现；应用层可注册数据库/队列实现覆盖）
/// </summary>
public class NoOpTelegramMessageAuditStore : ITelegramMessageAuditStore
{
    /// <inheritdoc />
    public Task AppendAsync(TelegramMessageAuditRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
