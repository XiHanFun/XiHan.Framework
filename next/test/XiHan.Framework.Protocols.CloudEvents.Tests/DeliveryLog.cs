// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Handlers;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 记录处理器收到的投递与当时的 Baggage。
/// </summary>
/// <remarks>
/// 发布模式下投递是异步的，用 <see cref="WaitAsync"/> 等待。
/// </remarks>
internal sealed class DeliveryLog
{
    /// <summary>
    /// 保护记录列表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 记录。
    /// </summary>
    private readonly List<(MessageDelivery Delivery, IReadOnlyDictionary<string, string> Baggage)> _records = [];

    /// <summary>
    /// 已记录的投递快照。
    /// </summary>
    public IReadOnlyList<(MessageDelivery Delivery, IReadOnlyDictionary<string, string> Baggage)> Records
    {
        get
        {
            lock (_lock)
            {
                return _records.ToList();
            }
        }
    }

    /// <summary>
    /// 记录一次投递。
    /// </summary>
    public void Record(MessageDelivery delivery, IReadOnlyDictionary<string, string> baggage)
    {
        lock (_lock)
        {
            _records.Add((delivery, new Dictionary<string, string>(baggage, StringComparer.Ordinal)));
        }
    }

    /// <summary>
    /// 等到记录数达到 <paramref name="count"/>。
    /// </summary>
    /// <remarks>
    /// 超时抛出。
    /// </remarks>
    public async Task WaitAsync(int count, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (Records.Count < count)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {count} deliveries but got {Records.Count}.");
            }

            await Task.Delay(20);
        }
    }
}
