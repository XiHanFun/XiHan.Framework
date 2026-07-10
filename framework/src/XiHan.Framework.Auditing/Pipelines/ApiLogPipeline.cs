#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApiLogPipeline
// Guid:08192031-2345-6789-0123-def123456789
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Queues;
using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Pipelines;

/// <summary>
/// 接口日志管道
/// </summary>
public class ApiLogPipeline : IApiLogPipeline
{
    private readonly IApiLogWriter _writer;
    private readonly ILogQueue<ApiLogRecord> _queue;
    private readonly XiHanAuditingLogQueueOptions _options;
    private readonly ILogger<ApiLogPipeline> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="queue"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public ApiLogPipeline(
        IApiLogWriter writer,
        ILogQueue<ApiLogRecord> queue,
        IOptions<XiHanAuditingLogQueueOptions> options,
        ILogger<ApiLogPipeline> logger)
    {
        _writer = writer;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteAsync(ApiLogRecord record, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableApiLogQueue)
        {
            await _writer.WriteAsync(record, CancellationToken.None);
            return;
        }

        // 满时丢弃：只尝试一次，不等待
        if (_options.DropOnFull)
        {
            if (!_queue.TryEnqueue(record))
            {
                _logger.LogWarning("接口日志队列已满，丢弃日志，TraceId: {TraceId}", record.TraceId);
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
