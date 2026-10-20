// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

public sealed class MaybeTests
{
    [Fact]
    public void Some_HasValue()
    {
        var maybe = Maybe.Some("x");

        Assert.True(maybe.HasValue);
        Assert.True(maybe.TryGetValue(out var value));
        Assert.Equal("x", value);
        Assert.Equal("x", maybe.ValueOrThrow());
        Assert.Equal("Some(x)", maybe.ToString());
    }

    [Fact]
    public void None_AndDefault_AreEqualAndEmpty()
    {
        Maybe<string> none = default;

        Assert.Equal(Maybe.None<string>(), none);
        Assert.False(none.HasValue);
        Assert.False(none.TryGetValue(out _));
        Assert.Equal("fallback", none.ValueOr("fallback"));
        Assert.Throws<InvalidOperationException>(() => none.ValueOrThrow());
        Assert.Equal("None", none.ToString());
    }

    [Fact]
    public void Some_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Maybe.Some<string>(null!));
    }

    [Fact]
    public void From_MapsNullToNone()
    {
        Assert.False(Maybe.From((string?)null).HasValue);
        Assert.True(Maybe.From("x").HasValue);
        Assert.False(Maybe.From((int?)null).HasValue);
        Assert.Equal(3, Maybe.From((int?)3).ValueOrThrow());
    }

    [Fact]
    public void Map_And_Bind_PropagateNone()
    {
        var some = Maybe.Some(2);
        var none = Maybe.None<int>();

        Assert.Equal(4, some.Map(static v => v * 2).ValueOrThrow());
        Assert.False(none.Map(static v => v * 2).HasValue);
        Assert.Equal("2", some.Bind(static v => Maybe.Some(v.ToString(System.Globalization.CultureInfo.InvariantCulture))).ValueOrThrow());
        Assert.False(none.Bind(static v => Maybe.Some(v)).HasValue);
    }

    [Fact]
    public void Match_SelectsBranch()
    {
        Assert.Equal("some", Maybe.Some(1).Match(static _ => "some", static () => "none"));
        Assert.Equal("none", Maybe.None<int>().Match(static _ => "some", static () => "none"));
    }

    [Fact]
    public void ToResult_UsesErrorForNone()
    {
        var error = Error.NotFound("ORDERS_NOTFOUND_001", "订单不存在");

        Assert.Equal(1, Maybe.Some(1).ToResult(error).ValueOrThrow());
        Assert.Same(error, Maybe.None<int>().ToResult(error).Error);
    }
}
