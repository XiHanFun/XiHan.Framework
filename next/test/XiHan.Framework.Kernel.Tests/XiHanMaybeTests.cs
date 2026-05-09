// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Xunit;

namespace XiHan.Framework.Kernel.Tests;

public class XiHanMaybeTests
{
    [Fact]
    public void Some_ShouldHaveValue()
    {
        var maybe = XiHanMaybe.Some("hello");
        Assert.True(maybe.HasValue);
        Assert.Equal("hello", maybe.Value);
    }

    [Fact]
    public void None_ShouldNotHaveValue()
    {
        var maybe = XiHanMaybe.None<int>();
        Assert.False(maybe.HasValue);
        Assert.Throws<InvalidOperationException>(() => maybe.Value);
    }

    [Fact]
    public void Map_OnSome_ShouldTransform()
    {
        var result = XiHanMaybe.Some(5).Map(x => x * 2);
        Assert.True(result.HasValue);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void Map_OnNone_ShouldStayNone()
    {
        var result = XiHanMaybe.None<int>().Map(x => x + 1);
        Assert.False(result.HasValue);
    }

    [Fact]
    public void Bind_OnSome_ShouldChain()
    {
        var result = XiHanMaybe.Some(3)
            .Bind(x => x > 0 ? XiHanMaybe.Some("ok") : XiHanMaybe.None<string>());
        Assert.True(result.HasValue);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void Bind_OnNone_ShouldStayNone()
    {
        var result = XiHanMaybe.None<int>().Bind(x => XiHanMaybe.Some("nope"));
        Assert.False(result.HasValue);
    }

    [Fact]
    public void Match_ShouldHandleBothPaths()
    {
        var some = XiHanMaybe.Some(7).Match(v => v * 3, () => -1);
        Assert.Equal(21, some);

        var none = XiHanMaybe.None<int>().Match(v => v, () => -1);
        Assert.Equal(-1, none);
    }

    [Fact]
    public void ValueOrDefault_ShouldReturnDefaultOnNone()
    {
        Assert.Equal(42, XiHanMaybe.None<int>().ValueOrDefault(42));
        Assert.Equal(10, XiHanMaybe.Some(10).ValueOrDefault(42));
    }

    [Fact]
    public void OrElse_ShouldUseFactoryOnNone()
    {
        Assert.Equal(99, XiHanMaybe.None<int>().OrElse(() => 99));
        Assert.Equal(5, XiHanMaybe.Some(5).OrElse(() => 99));
    }

    [Fact]
    public void ImplicitConversion_FromValue()
    {
        XiHanMaybe<string> maybe = "world";
        Assert.True(maybe.HasValue);
        Assert.Equal("world", maybe.Value);
    }

    [Fact]
    public void Equality_TwoSomesWithSameValue_ShouldBeEqual()
    {
        var a = XiHanMaybe.Some(1);
        var b = XiHanMaybe.Some(1);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_SomeAndNone_ShouldNotBeEqual()
    {
        var a = XiHanMaybe.Some(1);
        var b = XiHanMaybe.None<int>();
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void ToString_Some_ShouldDisplayValue()
    {
        Assert.Equal("Some(42)", XiHanMaybe.Some(42).ToString());
    }

    [Fact]
    public void ToString_None_ShouldDisplayNone()
    {
        Assert.Equal("None", XiHanMaybe.None<int>().ToString());
    }
}
