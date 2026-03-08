#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ChannelLogQueue
// Guid:1b1783d2-55c2-4c6f-8b1f-8c62f2d65f2c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Threading.Channels;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Api.Logging.Options;

namespace XiHan.Framework.Web.Api.Logging.Queues;

/// <summary>
/// 基于 Channel 的日志队列
/// </summary>
/// <typeparam name="TRecord"></typeparam>
public class ChannelLogQueue<TRecord> : ILogQueue<TRecord>
{
    private readonly Channel<TRecord> _channel;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    public ChannelLogQueue(IOptions<XiHanWebApiLogQueueOptions> options)
    {
        var queueOptions = options.Value;
        var boundedOptions = new BoundedChannelOptions(queueOptions.QueueCapacity)
        {
            FullMode = queueOptions.DropOnFull ? BoundedChannelFullMode.DropWrite : BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<TRecord>(boundedOptions);
    }

    /// <inheritdoc />
    public int Count => _channel.Reader.Count;

    /// <inheritdoc />
    public bool TryEnqueue(TRecord record)
    {
        return _channel.Writer.TryWrite(record);
    }

    /// <inheritdoc />
    public ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(record, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TRecord> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
