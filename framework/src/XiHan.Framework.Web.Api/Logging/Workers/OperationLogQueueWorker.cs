#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OperationLogQueueWorker
// Guid:1c58a51d-ff6b-4bbf-9a78-bb1d0c6b9f57
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Api.Logging.Options;
using XiHan.Framework.Web.Api.Logging.Queues;
using XiHan.Framework.Web.Api.Logging.Writers;

namespace XiHan.Framework.Web.Api.Logging.Workers;

/// <summary>
/// 操作日志队列消费者
/// </summary>
public class OperationLogQueueWorker : BackgroundService
{
    private readonly ILogQueue<OperationLogRecord> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XiHanWebApiLogQueueOptions _options;
    private readonly ILogger<OperationLogQueueWorker> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="queue"></param>
    /// <param name="scopeFactory"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public OperationLogQueueWorker(
        ILogQueue<OperationLogRecord> queue,
        IServiceScopeFactory scopeFactory,
        IOptions<XiHanWebApiLogQueueOptions> options,
        ILogger<OperationLogQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableOperationLogQueue)
        {
            return;
        }

        var batch = new List<OperationLogRecord>(_options.BatchSize);
        var lastFlushAt = DateTimeOffset.UtcNow;

        try
        {
            await foreach (var record in _queue.DequeueAllAsync(stoppingToken))
            {
                batch.Add(record);

                if (batch.Count >= _options.BatchSize ||
                    (DateTimeOffset.UtcNow - lastFlushAt).TotalMilliseconds >= _options.BatchDelayMilliseconds)
                {
                    await FlushAsync(batch, CancellationToken.None);
                    batch.Clear();
                    lastFlushAt = DateTimeOffset.UtcNow;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // host 正常停止，忽略取消异常
        }

        if (batch.Count > 0)
        {
            await FlushAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushAsync(List<OperationLogRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var writer = scope.ServiceProvider.GetService<IOperationLogWriter>();
            if (writer is null)
            {
                return;
            }

            foreach (var record in batch)
            {
                await writer.WriteAsync(record, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 由外部取消触发，视为正常结束
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "操作日志队列消费失败");
        }
    }
}
