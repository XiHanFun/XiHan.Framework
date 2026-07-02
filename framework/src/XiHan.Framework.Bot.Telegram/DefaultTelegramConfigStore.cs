#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultTelegramConfigStore
// Guid:5f86b3b2-9b61-4ac6-982a-00e9c19b591f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Bot.Telegram;

/// <summary>
/// 默认 Telegram 配置存储（基于选项）
/// </summary>
public class DefaultTelegramConfigStore : ITelegramConfigStore
{
    private readonly IOptionsMonitor<TelegramOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">Telegram 选项监视器</param>
    public DefaultTelegramConfigStore(IOptionsMonitor<TelegramOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public Task<TelegramOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<TelegramOptions?>(_options.CurrentValue);
    }
}
