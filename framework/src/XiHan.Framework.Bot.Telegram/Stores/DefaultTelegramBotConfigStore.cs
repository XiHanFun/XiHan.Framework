// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
