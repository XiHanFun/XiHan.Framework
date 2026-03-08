#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OperationLogPipeline
// Guid:6e0f8c71-0f05-4b5c-8d4f-0c7f1a2cf013
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
/// 操作日志管道
/// </summary>
public class OperationLogPipeline : IOperationLogPipeline
{
    private readonly IOperationLogWriter _writer;
    private readonly ILogQueue<OperationLogRecord> _queue;
    private readonly XiHanWebApiLogQueueOptions _options;
    private readonly ILogger<OperationLogPipeline> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="queue"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public OperationLogPipeline(
        IOperationLogWriter writer,
        ILogQueue<OperationLogRecord> queue,
        IOptions<XiHanWebApiLogQueueOptions> options,
        ILogger<OperationLogPipeline> logger)
    {
        _writer = writer;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteAsync(OperationLogRecord record, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableOperationLogQueue)
        {
            await _writer.WriteAsync(record, cancellationToken);
            return;
        }

        if (_options.DropOnFull && !_queue.TryEnqueue(record))
        {
            _logger.LogWarning("操作日志队列已满，丢弃日志，TraceId: {TraceId}", record.TraceId);
            return;
        }

        await _queue.EnqueueAsync(record, cancellationToken);
    }
}
