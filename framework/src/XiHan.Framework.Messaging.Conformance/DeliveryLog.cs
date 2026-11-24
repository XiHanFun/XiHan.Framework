// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Threading.Channels;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// 套件的到达日志
/// </summary>
/// <remarks>
/// 处理器把每次投递写进来，用例按条件等待。可指定某些事件 ID 失败、抛异常或一直等到被取消（每次投递都等，或只在某一次尝试上等）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DeliveryLog
{
    /// <summary>
    /// 到达通知通道
    /// </summary>
    /// <remarks>
    /// <see cref="WaitAsync"/> 靠它被唤醒
    /// </remarks>
    private readonly Channel<ReceivedMessage> _received = Channel.CreateUnbounded<ReceivedMessage>();

    /// <summary>
    /// 全部到达，按记录顺序
    /// </summary>
    private readonly List<ReceivedMessage> _all = [];

    /// <summary>
    /// 保护到达列表的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 处理时返回失败的事件 ID
    /// </summary>
    public ConcurrentDictionary<string, byte> FailingIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 处理时抛异常的事件 ID
    /// </summary>
    public ConcurrentDictionary<string, byte> ThrowingIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 处理时一直等到调用被取消的事件 ID
    /// </summary>
    /// <remarks>
    /// 模拟代理停止时还在处理的投递：处理器记下到达后等在调用的取消令牌上。从表里移除之后到达的投递照常处理
    /// </remarks>
    public ConcurrentDictionary<string, byte> BlockingIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 只在给定的尝试号上一直等到调用被取消的事件 ID
    /// </summary>
    /// <remarks>
    /// 值是尝试号（从 1 起）：这个 ID 的投递只有尝试号与它相同的那一次等在调用的取消令牌上，别的尝试照 <see cref="FailingIds"/> 与 <see cref="ThrowingIds"/> 处理。与 <see cref="FailingIds"/> 合用可以让前几次尝试失败、让代理停止时正好打断最后一次尝试
    /// </remarks>
    public ConcurrentDictionary<string, int> BlockingAttempts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 全部到达，按顺序
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
    /// 记录
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
    /// 等到满足条件的到达累计 <paramref name="count"/> 条
    /// </summary>
    /// <remarks>
    /// 超时抛出
    /// </remarks>
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
