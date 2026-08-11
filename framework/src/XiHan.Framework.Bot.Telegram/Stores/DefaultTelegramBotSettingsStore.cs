// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Stores;

/// <summary>
/// 默认 Telegram 机器人平台全局设置存储（基于选项，配置文件兜底）
/// </summary>
public class DefaultTelegramBotSettingsStore : ITelegramBotSettingsStore
{
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">平台选项监视器</param>
    public DefaultTelegramBotSettingsStore(IOptionsMonitor<TelegramBotPlatformOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 获取当前生效的平台全局设置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>选项监视器当前值中的平台全局设置</returns>
    public Task<TelegramBotSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_options.CurrentValue.Settings);
    }
}
