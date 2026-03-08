#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AccessLogPipeline
// Guid:8bbd5c33-ccf5-4d0d-95f6-41b86b0f9a59
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Api.Logging.Options;
using XiHan.Framework.Web.Api.Logging.Queues;

namespace XiHan.Framework.Web.Api.Logging.Pipelines;

/// <summary>
/// 访问日志管道
/// </summary>
public class AccessLogPipeline : IAccessLogPipeline
{
    private readonly IAccessLogWriter _writer;
    private readonly ILogQueue<AccessLogRecord> _queue;
    private readonly XiHanWebApiLogQueueOptions _options;
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
        IOptions<XiHanWebApiLogQueueOptions> options,
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
            await _writer.WriteAsync(record, cancellationToken);
            return;
        }

        if (_options.DropOnFull && !_queue.TryEnqueue(record))
        {
            _logger.LogWarning("访问日志队列已满，丢弃日志，TraceId: {TraceId}", record.TraceId);
            return;
        }

        await _queue.EnqueueAsync(record, cancellationToken);
    }
}
