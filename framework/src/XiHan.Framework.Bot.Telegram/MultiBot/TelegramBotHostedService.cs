// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Bot.Telegram.MultiBot;

/// <summary>
/// Telegram 机器人宿主服务（随应用生命周期启动/停止管理器；启动失败吞异常记日志，不阻断应用）
/// </summary>
public sealed class TelegramBotHostedService : IHostedService
{
    private readonly TelegramBotManager _manager;
    private readonly ILogger<TelegramBotHostedService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="manager">机器人管理器</param>
    /// <param name="logger">日志记录器</param>
    public TelegramBotHostedService(TelegramBotManager manager, ILogger<TelegramBotHostedService> logger)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _manager.StartAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram Bot 后台启动失败。");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _manager.StopAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram Bot 后台停止失败。");
        }
    }
}
