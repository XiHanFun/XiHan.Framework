// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Stores;

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
