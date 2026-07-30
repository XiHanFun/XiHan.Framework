// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Channels;
using Microsoft.Extensions.Options;
using XiHan.Framework.Auditing.Options;

namespace XiHan.Framework.Auditing.Queues;

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
    /// <remarks>
    /// 固定使用 <see cref="BoundedChannelFullMode.Wait"/>：队列满时 <see cref="TryEnqueue"/> 返回 false、
    /// <see cref="EnqueueAsync"/> 等待。满时丢弃与否是调用方（采集管道）按 <c>DropOnFull</c> 选择哪个方法的策略，
    /// 不能下沉到 Channel——若这里用 DropWrite，TryWrite 满时也返回 true，调用方便无从得知记录已被丢弃。
    /// </remarks>
    public ChannelLogQueue(IOptions<XiHanAuditingLogQueueOptions> options)
    {
        var queueOptions = options.Value;
        var boundedOptions = new BoundedChannelOptions(queueOptions.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
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
