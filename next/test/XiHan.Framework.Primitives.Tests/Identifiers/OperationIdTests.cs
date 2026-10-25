// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests.Identifiers;

/// <summary>
/// 操作 ID 文法与分段。
/// </summary>
public sealed class OperationIdTests
{
    [Theory]
    [InlineData("orders.create", true)]
    [InlineData("order-lines.list", true)]
    [InlineData("orders.cancel-all", true)]
    [InlineData("v2orders.get", true)]
    [InlineData("orders", false)]
    [InlineData("Orders.create", false)]
    [InlineData("orders.Create", false)]
    [InlineData("orders..create", false)]
    [InlineData("orders.create.now", false)]
    [InlineData("-orders.create", false)]
    [InlineData("orders-.create", false)]
    [InlineData("orders_lines.create", false)]
    [InlineData("", false)]
    public void IsValid_FollowsGrammar(string id, bool valid)
    {
        Assert.Equal(valid, OperationId.IsValid(id));
        if (valid)
        {
            Assert.Equal(id, OperationId.Validate(id));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => OperationId.Validate(id));
        }
    }

    [Fact]
    public void ResourceOf_AndVerbOf_SplitSegments()
    {
        Assert.Equal("order-lines", OperationId.ResourceOf("order-lines.list"));
        Assert.Equal("list", OperationId.VerbOf("order-lines.list"));
        Assert.False(OperationId.IsValid(null));
        Assert.Throws<ArgumentNullException>(static () => OperationId.Validate(null!));
        Assert.Throws<ArgumentException>(static () => OperationId.ResourceOf("bad"));
    }
}
