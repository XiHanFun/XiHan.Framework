// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Domain.Tests;

/// <summary>
/// <see cref="DomainEventBuffer"/> 的记录顺序、清空、容量上限与参数校验。
/// </summary>
public sealed class DomainEventBufferTests
{
    /// <summary>
    /// 只携带发生时间的测试事件。
    /// </summary>
    /// <param name="OccurredAt">发生时间。</param>
    private sealed record Happened(DateTimeOffset OccurredAt) : IDomainEvent;

    [Fact]
    public void Add_RecordsInOrder_AndClearEmpties()
    {
        var events = new DomainEventBuffer();
        var first = new Happened(DateTimeOffset.UnixEpoch);
        var second = new Happened(DateTimeOffset.UnixEpoch.AddSeconds(1));

        events.Add(first);
        events.Add(second);

        Assert.Equal([first, second], events.Items);

        events.Clear();

        Assert.Empty(events.Items);
    }

    [Fact]
    public void Add_BeyondCapacity_Throws()
    {
        var events = new DomainEventBuffer(capacity: 2);
        events.Add(new Happened(DateTimeOffset.UnixEpoch));
        events.Add(new Happened(DateTimeOffset.UnixEpoch));

        // 超限的事件被拒绝且不会被部分记录
        Assert.Throws<InvalidOperationException>(() => events.Add(new Happened(DateTimeOffset.UnixEpoch)));
        Assert.Equal(2, events.Items.Count);
    }

    [Fact]
    public void Constructor_And_Add_RejectInvalidInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DomainEventBuffer(0));
        Assert.Throws<ArgumentNullException>(static () => new DomainEventBuffer().Add(null!));
        Assert.Equal(256, DomainEventBuffer.DefaultCapacity);
    }
}
