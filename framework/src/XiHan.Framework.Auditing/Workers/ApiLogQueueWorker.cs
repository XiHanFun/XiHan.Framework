// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Queues;
using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Workers;

/// <summary>
/// 接口日志队列消费者
/// </summary>
public class ApiLogQueueWorker : BackgroundService
{
    private readonly ILogQueue<ApiLogRecord> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XiHanAuditingLogQueueOptions _options;
    private readonly ILogger<ApiLogQueueWorker> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="queue"></param>
    /// <param name="scopeFactory"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public ApiLogQueueWorker(
        ILogQueue<ApiLogRecord> queue,
        IServiceScopeFactory scopeFactory,
        IOptions<XiHanAuditingLogQueueOptions> options,
        ILogger<ApiLogQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableApiLogQueue)
        {
            return;
        }

        var batch = new List<ApiLogRecord>(_options.BatchSize);
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

    private async Task FlushAsync(List<ApiLogRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var writer = scope.ServiceProvider.GetService<IApiLogWriter>();
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
            _logger.LogWarning(ex, "接口日志队列消费失败");
        }
    }
}
