#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MessageOutboxBackgroundService
// Guid:f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Options;

namespace XiHan.Framework.Messaging.Services;

/// <summary>
/// 消息发件箱后台服务
/// </summary>
public class MessageOutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly XiHanMessagingOptions _options;
    private readonly ILogger<MessageOutboxBackgroundService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MessageOutboxBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<XiHanMessagingOptions> options,
        ILogger<MessageOutboxBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 执行后台轮询
    /// </summary>
    /// <param name="stoppingToken">停止令牌</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.OutboxEnabled)
        {
            _logger.LogDebug("消息发件箱未启用，后台服务将不执行任何操作");
            return;
        }

        _logger.LogInformation("消息发件箱后台服务已启动，轮询间隔: {Interval}秒, 批次大小: {BatchSize}",
            _options.OutboxPollIntervalSeconds, _options.OutboxBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IMessageOutboxProcessor>();
                await processor.ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 预期内的取消，正常退出
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "消息发件箱后台服务轮询异常");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.OutboxPollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 预期内的取消，正常退出
                break;
            }
        }

        _logger.LogInformation("消息发件箱后台服务已停止");
    }
}
