// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Xunit;

namespace XiHan.Framework.Kernel.Tests;

public class XiHanResultTests
{
    [Fact]
    public void Success_ShouldReturnIsSuccessTrue()
    {
        var result = XiHanResult<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_ShouldReturnIsFailureTrue()
    {
        var error = XiHanError.Validation("invalid");
        var result = XiHanResult<int>.Failure(error);
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.Error.Code);
    }

    [Fact]
    public void Value_OnFailure_ShouldThrow()
    {
        var result = XiHanResult<int>.Failure(XiHanError.Unexpected("boom"));
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_OnSuccess_ShouldThrow()
    {
        var result = XiHanResult<int>.Success(1);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Map_OnSuccess_ShouldTransform()
    {
        var result = XiHanResult<int>.Success(10).Map(x => x * 2);
        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Map_OnFailure_ShouldPropagateError()
    {
        var error = XiHanError.NotFound("gone");
        var result = XiHanResult<int>.Failure(error).Map(x => x + 1);
        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public void Bind_OnSuccess_ShouldChain()
    {
        var result = XiHanResult<int>.Success(5)
            .Bind(x => x > 0 ? XiHanResult<string>.Success($"ok:{x}") : XiHanResult<string>.Failure(XiHanError.Validation("negative")));
        Assert.True(result.IsSuccess);
        Assert.Equal("ok:5", result.Value);
    }

    [Fact]
    public void Bind_OnFailure_ShouldShortCircuit()
    {
        var result = XiHanResult<int>.Failure(XiHanError.Unexpected("fail"))
            .Bind(x => XiHanResult<string>.Success("unreachable"));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void MapError_OnFailure_ShouldTransformError()
    {
        var result = XiHanResult<int>.Failure(XiHanError.Validation("old"))
            .MapError(_ => XiHanError.Unexpected("wrapped"));
        Assert.True(result.IsFailure);
        Assert.Equal("UNEXPECTED", result.Error.Code);
    }

    [Fact]
    public void Match_ShouldHandleBothPaths()
    {
        var success = XiHanResult<int>.Success(3).Match(v => v * 2, _ => -1);
        Assert.Equal(6, success);

        var failure = XiHanResult<int>.Failure(XiHanError.NotFound("nope")).Match(v => v, _ => -1);
        Assert.Equal(-1, failure);
    }

    [Fact]
    public void ImplicitConversion_FromValue()
    {
        XiHanResult<string> result = "hello";
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError()
    {
        XiHanResult<string> result = XiHanError.NotFound("gone");
        Assert.True(result.IsFailure);
    }
}
