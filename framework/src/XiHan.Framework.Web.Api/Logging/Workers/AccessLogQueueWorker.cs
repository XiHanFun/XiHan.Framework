#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AccessLogQueueWorker
// Guid:9f2a8f62-5ed0-4a3a-9710-2f743ef7aa95
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Api.Logging.Options;
using XiHan.Framework.Web.Api.Logging.Queues;

namespace XiHan.Framework.Web.Api.Logging.Workers;

/// <summary>
/// 访问日志队列消费者
/// </summary>
public class AccessLogQueueWorker : BackgroundService
{
    private readonly ILogQueue<AccessLogRecord> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XiHanWebApiLogQueueOptions _options;
    private readonly ILogger<AccessLogQueueWorker> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="queue"></param>
    /// <param name="scopeFactory"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public AccessLogQueueWorker(
        ILogQueue<AccessLogRecord> queue,
        IServiceScopeFactory scopeFactory,
        IOptions<XiHanWebApiLogQueueOptions> options,
        ILogger<AccessLogQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableAccessLogQueue)
        {
            return;
        }

        var batch = new List<AccessLogRecord>(_options.BatchSize);
        var lastFlushAt = DateTimeOffset.UtcNow;

        await foreach (var record in _queue.DequeueAllAsync(stoppingToken))
        {
            batch.Add(record);

            if (batch.Count >= _options.BatchSize ||
                (DateTimeOffset.UtcNow - lastFlushAt).TotalMilliseconds >= _options.BatchDelayMilliseconds)
            {
                await FlushAsync(batch, stoppingToken);
                batch.Clear();
                lastFlushAt = DateTimeOffset.UtcNow;
            }
        }

        if (batch.Count > 0)
        {
            await FlushAsync(batch, stoppingToken);
        }
    }

    private async Task FlushAsync(List<AccessLogRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var writer = scope.ServiceProvider.GetService<IAccessLogWriter>();
            if (writer is null)
            {
                return;
            }

            foreach (var record in batch)
            {
                await writer.WriteAsync(record, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "访问日志队列消费失败");
        }
    }
}
