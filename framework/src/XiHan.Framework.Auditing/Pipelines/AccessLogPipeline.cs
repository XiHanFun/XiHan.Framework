// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Queues;
using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Pipelines;

/// <summary>
/// 访问日志管道
/// </summary>
public class AccessLogPipeline : IAccessLogPipeline
{
    private readonly IAccessLogWriter _writer;
    private readonly ILogQueue<AccessLogRecord> _queue;
    private readonly XiHanAuditingLogQueueOptions _options;
    private readonly ILogger<AccessLogPipeline> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="queue"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public AccessLogPipeline(
        IAccessLogWriter writer,
        ILogQueue<AccessLogRecord> queue,
        IOptions<XiHanAuditingLogQueueOptions> options,
        ILogger<AccessLogPipeline> logger)
    {
        _writer = writer;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteAsync(AccessLogRecord record, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableAccessLogQueue)
        {
            await _writer.WriteAsync(record, CancellationToken.None);
            return;
        }

        // 满时丢弃：只尝试一次，不等待
        if (_options.DropOnFull)
        {
            if (!_queue.TryEnqueue(record))
            {
                _logger.LogWarning("访问日志队列已满，丢弃日志，TraceId: {TraceId}", record.TraceId);
            }

            return;
        }

        // 满时等待：反压到调用方
        try
        {
            await _queue.EnqueueAsync(record, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 请求取消时忽略日志入队取消，避免污染主流程日志
        }
    }
}
