// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

public sealed class TypedIdTests
{
    private sealed class Order;

    private sealed class Customer;

    [Fact]
    public void SameTagAndValue_AreEqual()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new TypedId<Order, Guid>(id), new TypedId<Order, Guid>(id));
        Assert.NotEqual(new TypedId<Order, Guid>(id), new TypedId<Order, Guid>(Guid.NewGuid()));
    }

    [Fact]
    public void DifferentTags_AreDifferentTypes()
    {
        var id = Guid.NewGuid();
        object order = new TypedId<Order, Guid>(id);
        object customer = new TypedId<Customer, Guid>(id);

        Assert.NotEqual(order, customer);
        Assert.IsAssignableFrom<ITypedId<Guid>>(order);
    }

    [Fact]
    public void ToString_UsesUnderlyingValue()
    {
        Assert.Equal("1001", new TypedId<Order, long>(1001).ToString());
    }
}
