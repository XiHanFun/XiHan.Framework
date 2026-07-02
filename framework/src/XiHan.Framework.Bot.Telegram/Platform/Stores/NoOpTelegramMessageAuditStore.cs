#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NoOpTelegramMessageAuditStore
// Guid:8a77f72a-0637-45ee-8aac-04b87cbbf6d3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Platform.Abstractions;

namespace XiHan.Framework.Bot.Telegram.Platform.Stores;

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
