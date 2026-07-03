#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultTelegramBotConfigStore
// Guid:4ef892d0-0d03-4143-838e-8fcde7db2d9a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Stores;

/// <summary>
/// 默认 Telegram 机器人配置存储（基于选项，配置文件兜底）
/// </summary>
public class DefaultTelegramBotConfigStore : ITelegramBotConfigStore
{
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">平台选项监视器</param>
    public DefaultTelegramBotConfigStore(IOptionsMonitor<TelegramBotPlatformOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TelegramBotConfig>> GetBotConfigsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TelegramBotConfig> configs = [.. _options.CurrentValue.Bots];
        return Task.FromResult(configs);
    }
}
