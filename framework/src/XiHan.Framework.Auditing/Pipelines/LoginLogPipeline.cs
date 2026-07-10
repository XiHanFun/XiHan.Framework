#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LoginLogPipeline
// Guid:ffeadbf6-c5c3-4178-9f33-7162fb83ae87
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/03 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
