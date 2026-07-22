// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Queues;
using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Pipelines;

/// <summary>
/// 登录日志管道
/// </summary>
public class LoginLogPipeline : ILoginLogPipeline
{
    private readonly ILoginLogWriter _writer;
    private readonly ILogQueue<LoginLogRecord> _queue;
    private readonly XiHanAuditingLogQueueOptions _options;
    private readonly ILogger<LoginLogPipeline> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LoginLogPipeline(
        ILoginLogWriter writer,
        ILogQueue<LoginLogRecord> queue,
        IOptions<XiHanAuditingLogQueueOptions> options,
        ILogger<LoginLogPipeline> logger)
    {
        _writer = writer;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteAsync(LoginLogRecord record, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLoginLogQueue)
        {
            await _writer.WriteAsync(record, CancellationToken.None);
            return;
        }

        // 满时丢弃：只尝试一次，不等待
        if (_options.DropOnFull)
        {
            if (!_queue.TryEnqueue(record))
            {
                _logger.LogWarning("登录日志队列已满，丢弃日志，TraceId: {TraceId}", record.TraceId);
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
            // 请求取消时忽略日志入队取消
        }
    }
}
