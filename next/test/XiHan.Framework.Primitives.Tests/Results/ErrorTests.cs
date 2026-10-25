// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests.Results;

/// <summary>
/// <see cref="Error"/> 与 <see cref="ErrorCode"/> 的行为：格式校验、KIND 段约束、相等语义与文本形式。
/// </summary>
public sealed class ErrorTests
{
    /// <summary>
    /// <c>error-codes.json</c> 向量：合法与非法的错误码。
    /// </summary>
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
        var exception = Assert.Throws<ArgumentException>(() => Error.Literal("bad-code", "message"));

        Assert.Equal("code", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithBlankMessage_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Error.Literal("KERNEL_UNEXPECTED_001", " "));
    }

    [Fact]
    public void Factories_EnforceKindSegment()
    {
        var error = Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found");

        Assert.Equal("NOTFOUND", error.Kind);
        Assert.Throws<ArgumentException>(() => Error.NotFound("ORDERS_CONFLICT_001", "orders.order.not_found"));
        Assert.Throws<ArgumentException>(() => Error.Conflict("ORDERS_NOTFOUND_001", "orders.order.conflict"));
    }

    [Fact]
    public void Equality_IgnoresDetailsIdentityOnlyWhenSameReference()
    {
        var details = new Dictionary<string, object?> { ["field"] = "name" };
        var left = Error.Literal("ORDERS_VALIDATION_001", "Missing field.", details);
        var right = Error.Literal("ORDERS_VALIDATION_001", "Missing field.", details);
        var other = Error.Literal("ORDERS_VALIDATION_001", "Missing field.", new Dictionary<string, object?> { ["field"] = "name" });

        Assert.Equal(left, right);
        Assert.NotEqual(left, other);
    }

    [Fact]
    public void ToString_FormatsCodeAndMessage()
    {
        var error = Error.Literal("ORDERS_CONFLICT_002", "Order is closed.");

        Assert.Equal("[ORDERS_CONFLICT_002] Order is closed.", error.ToString());
    }

    [Fact]
    public void Unexpected_CarriesInnerError()
    {
        var inner = Error.Literal("PERSISTENCE_UNAVAILABLE_001", "Database unavailable.");
        var outer = Error.Literal("ORDERS_UNEXPECTED_001", "Failed to create order.", inner: inner);

        Assert.Same(inner, outer.Inner);
    }
}
