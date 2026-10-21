// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Domain.Tests;

public sealed class DomainEventBufferTests
{
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
