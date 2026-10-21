// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

public sealed class ResultTests
{
    private static readonly Error _sampleError = Error.Conflict("ORDERS_CONFLICT_001", "订单已存在");

    [Fact]
    public void Success_IsSuccessWithoutError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
        Assert.Equal("Success", result.ToString());
    }

    [Fact]
    public void Failure_CarriesError()
    {
        Result result = _sampleError;

        Assert.True(result.IsFailure);
        Assert.Same(_sampleError, result.Error);
        Assert.Equal("Failure([ORDERS_CONFLICT_001] 订单已存在)", result.ToString());
    }

    [Fact]
    public void Default_IsUninitializedFailure()
    {
        Result result = default;

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.UninitializedResult, result.Error!.Code);
        var exception = Assert.Throws<FrameworkException>(() => result.ThrowIfFailure());
        Assert.Equal(ErrorCode.UninitializedResult, exception.Code);
    }

    [Fact]
    public void Failure_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void Match_SelectsBranch()
    {
        Assert.Equal("ok", Result.Success().Match(static () => "ok", static e => e.Code));
        Assert.Equal("ORDERS_CONFLICT_001", Result.Failure(_sampleError).Match(static () => "ok", static e => e.Code));
    }

    [Fact]
    public void Bind_ShortCircuitsOnFailure()
    {
        var called = false;
        var result = Result.Failure(_sampleError).Bind(() =>
        {
            called = true;
            return Result.Success();
        });

        Assert.False(called);
        Assert.Same(_sampleError, result.Error);
        Assert.True(Result.Success().Bind(Result.Success).IsSuccess);
    }

    [Fact]
    public void ThrowIfFailure_ThrowsFrameworkExceptionWithCode()
    {
        var exception = Assert.Throws<FrameworkException>(() => Result.Failure(_sampleError).ThrowIfFailure());

        Assert.Equal(_sampleError.Code, exception.Code);
        Assert.Equal(_sampleError.Message, exception.Message);
        Result.Success().ThrowIfFailure();
    }

    [Fact]
    public void Equality_ComparesStateAndError()
    {
        Assert.Equal(Result.Success(), Result.Success());
        Assert.Equal(Result.Failure(_sampleError), Result.Failure(_sampleError));
        Assert.NotEqual(Result.Success(), Result.Failure(_sampleError));
    }
}
