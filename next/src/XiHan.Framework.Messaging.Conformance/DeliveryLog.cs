// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Threading.Channels;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// 套件的到达日志：处理器把每次投递写进来，用例按条件等待。可指定某些事件 ID 失败或抛异常。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class DeliveryLog
{
    /// <summary>
    /// 到达通知通道；<see cref="WaitAsync"/> 靠它被唤醒。
    /// </summary>
    private readonly Channel<ReceivedMessage> _received = Channel.CreateUnbounded<ReceivedMessage>();

    /// <summary>
    /// 全部到达，按记录顺序。
    /// </summary>
    private readonly List<ReceivedMessage> _all = [];

    /// <summary>
    /// 保护到达列表的锁。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 处理时返回失败的事件 ID。
    /// </summary>
    public ConcurrentDictionary<string, byte> FailingIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 处理时抛异常的事件 ID。
    /// </summary>
    public ConcurrentDictionary<string, byte> ThrowingIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 全部到达，按顺序。
    /// </summary>
    public IReadOnlyList<ReceivedMessage> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _all];
            }
        }
    }

    /// <summary>
    /// 记录。
    /// </summary>
    public void Record(ReceivedMessage received)
    {
        ArgumentNullException.ThrowIfNull(received);
        lock (_gate)
        {
            _all.Add(received);
        }

        _received.Writer.TryWrite(received);
    }

    /// <summary>
    /// 等到满足条件的到达累计 <paramref name="count"/> 条；超时抛出。
    /// </summary>
    public async Task<IReadOnlyList<ReceivedMessage>> WaitAsync(Func<ReceivedMessage, bool> predicate, int count, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        using var cts = new CancellationTokenSource(timeout);
        // 先看快照，避免错过在等待前已到达的消息；之后每来一条重新计数，直到满足或超时
        var matched = All.Where(predicate).ToList();
        if (matched.Count >= count)
        {
            return matched;
        }

        try
        {
            await foreach (var received in _received.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
            {
                if (All.Count(predicate) >= count)
                {
                    return All.Where(predicate).ToList();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Did not receive {count} matching deliveries within {timeout}; {All.Count} deliveries arrived in total.");
        }

        return All.Where(predicate).ToList();
    }
}
