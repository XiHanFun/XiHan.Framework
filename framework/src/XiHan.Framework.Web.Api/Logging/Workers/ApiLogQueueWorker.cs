#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApiLogQueueWorker
// Guid:19203142-3456-7890-1234-ef1234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Api.Logging.Options;
using XiHan.Framework.Web.Api.Logging.Queues;
using XiHan.Framework.Web.Api.Logging.Writers;

namespace XiHan.Framework.Web.Api.Logging.Workers;

/// <summary>
/// 接口日志队列消费者
/// </summary>
public class ApiLogQueueWorker : BackgroundService
{
    private readonly ILogQueue<ApiLogRecord> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XiHanWebApiLogQueueOptions _options;
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
        IOptions<XiHanWebApiLogQueueOptions> options,
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
