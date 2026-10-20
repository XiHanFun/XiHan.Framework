// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

public sealed class ErrorTests
{
    private sealed record ErrorCodeVectors(string[] Valid, string[] Invalid);

    [Fact]
    public void ErrorCode_IsValid_MatchesConformanceVectors()
    {
        var vectors = ConformanceVectors.Load<ErrorCodeVectors>("error-codes.json");

        Assert.All(vectors.Valid, static code => Assert.True(ErrorCode.IsValid(code), code));
        Assert.All(vectors.Invalid, static code => Assert.False(ErrorCode.IsValid(code), code));
    }

    [Fact]
    public void ErrorCode_KindOfAndDomainOf_SplitSegments()
    {
        Assert.Equal("CONFLICT", ErrorCode.KindOf("PERSISTENCE_CONFLICT_001"));
        Assert.Equal("PERSISTENCE", ErrorCode.DomainOf("PERSISTENCE_CONFLICT_001"));
    }

    [Fact]
    public void Constructor_WithInvalidCode_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Error("bad-code", "message"));

        Assert.Equal("code", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithBlankMessage_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Error("KERNEL_UNEXPECTED_001", " "));
    }

    [Fact]
    public void Factories_EnforceKindSegment()
    {
        var error = Error.NotFound("ORDERS_NOTFOUND_001", "订单不存在");

        Assert.Equal("NOTFOUND", error.Kind);
        Assert.Throws<ArgumentException>(() => Error.NotFound("ORDERS_CONFLICT_001", "错误的种类"));
        Assert.Throws<ArgumentException>(() => Error.Validation("ORDERS_NOTFOUND_001", "错误的种类"));
    }

    [Fact]
    public void Equality_IgnoresDetailsIdentityOnlyWhenSameReference()
    {
        var details = new Dictionary<string, object?> { ["field"] = "name" };
        var left = new Error("ORDERS_VALIDATION_001", "缺少字段", details);
        var right = new Error("ORDERS_VALIDATION_001", "缺少字段", details);
        var other = new Error("ORDERS_VALIDATION_001", "缺少字段", new Dictionary<string, object?> { ["field"] = "name" });

        Assert.Equal(left, right);
        Assert.NotEqual(left, other);
    }

    [Fact]
    public void ToString_FormatsCodeAndMessage()
    {
        var error = Error.Conflict("ORDERS_CONFLICT_002", "订单已关闭");

        Assert.Equal("[ORDERS_CONFLICT_002] 订单已关闭", error.ToString());
    }

    [Fact]
    public void Unexpected_CarriesInnerError()
    {
        var inner = Error.Unavailable("PERSISTENCE_UNAVAILABLE_001", "数据库不可用");
        var outer = Error.Unexpected("ORDERS_UNEXPECTED_001", "创建订单失败", inner);

        Assert.Same(inner, outer.Inner);
    }
}
