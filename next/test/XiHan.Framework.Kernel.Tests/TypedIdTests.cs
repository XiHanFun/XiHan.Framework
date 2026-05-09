// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Xunit;

namespace XiHan.Framework.Kernel.Tests;

public class TypedIdTests
{
    [Fact]
    public void SameValue_ShouldBeEqual()
    {
        var id1 = new TypedId<Guid>(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var id2 = new TypedId<Guid>(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
    }

    [Fact]
    public void DifferentValue_ShouldNotBeEqual()
    {
        var id1 = new TypedId<int>(1);
        var id2 = new TypedId<int>(2);
        Assert.NotEqual(id1, id2);
        Assert.True(id1 != id2);
    }

    [Fact]
    public void CompareTo_ShouldWork()
    {
        Assert.True(new TypedId<int>(1) < new TypedId<int>(2));
        Assert.True(new TypedId<int>(2) > new TypedId<int>(1));
    }

    [Fact]
    public void ImplicitConversion_ToUnderlyingType()
    {
        TypedId<long> id = new(100L);
        long value = id;
        Assert.Equal(100L, value);
    }

    [Fact]
    public void NullValue_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new TypedId<string>(null!));
    }

    [Fact]
    public void ToString_ShouldReturnValueString()
    {
        var id = new TypedId<int>(42);
        Assert.Equal("42", id.ToString());
    }
}
