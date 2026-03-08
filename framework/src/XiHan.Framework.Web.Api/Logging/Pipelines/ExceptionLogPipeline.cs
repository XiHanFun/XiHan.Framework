#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExceptionLogPipeline
// Guid:87ab6f75-568b-4e86-8e8a-1d70ad3c3e8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Api.Logging.Options;
using XiHan.Framework.Web.Api.Logging.Queues;
using XiHan.Framework.Web.Api.Logging.Writers;

namespace XiHan.Framework.Web.Api.Logging.Pipelines;

/// <summary>
/// 异常日志管道
/// </summary>
public class ExceptionLogPipeline : IExceptionLogPipeline
{
    private readonly IExceptionLogWriter _writer;
    private readonly ILogQueue<ExceptionLogRecord> _queue;
    private readonly XiHanWebApiLogQueueOptions _options;
    private readonly ILogger<ExceptionLogPipeline> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="queue"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public ExceptionLogPipeline(
        IExceptionLogWriter writer,
        ILogQueue<ExceptionLogRecord> queue,
        IOptions<XiHanWebApiLogQueueOptions> options,
        ILogger<ExceptionLogPipeline> logger)
    {
        _writer = writer;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableExceptionLogQueue)
        {
            await _writer.WriteAsync(record, cancellationToken);
            return;
        }

        if (_options.DropOnFull && !_queue.TryEnqueue(record))
        {
            _logger.LogWarning("异常日志队列已满，丢弃日志，TraceId: {TraceId}", record.TraceId);
            return;
        }

        await _queue.EnqueueAsync(record, cancellationToken);
    }
}
