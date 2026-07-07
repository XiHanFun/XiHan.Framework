#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LoginLogQueueWorker
// Guid:45ab0efd-3bb3-4f7c-a7c9-8ca2d6ccdb65
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/03 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Queues;
using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Workers;

/// <summary>
/// 登录日志队列消费者
/// </summary>
public class LoginLogQueueWorker : BackgroundService
{
    private readonly ILogQueue<LoginLogRecord> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XiHanAuditingLogQueueOptions _options;
    private readonly ILogger<LoginLogQueueWorker> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LoginLogQueueWorker(
        ILogQueue<LoginLogRecord> queue,
        IServiceScopeFactory scopeFactory,
        IOptions<XiHanAuditingLogQueueOptions> options,
        ILogger<LoginLogQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableLoginLogQueue)
        {
            return;
        }

        var batch = new List<LoginLogRecord>(_options.BatchSize);
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
            // host 正常停止
        }

        if (batch.Count > 0)
        {
            await FlushAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushAsync(List<LoginLogRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var writer = scope.ServiceProvider.GetService<ILoginLogWriter>();
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
            // 由外部取消触发
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "登录日志队列消费失败");
        }
    }
}
