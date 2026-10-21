// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

public sealed class ResultOfTTests
{
    private static readonly Error _sampleError = Error.NotFound("ORDERS_NOTFOUND_001", "订单不存在");

    [Fact]
    public void Success_CarriesValue()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(42, value);
        Assert.Equal(42, result.ValueOrThrow());
        Assert.Equal("Success(42)", result.ToString());
    }

    [Fact]
    public void Success_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
    }

    [Fact]
    public void Failure_CarriesError()
    {
        Result<int> result = _sampleError;

        Assert.True(result.IsFailure);
        Assert.Same(_sampleError, result.Error);
        Assert.False(result.TryGetValue(out _));
        Assert.Equal(-1, result.ValueOr(-1));
        var exception = Assert.Throws<FrameworkException>(() => result.ValueOrThrow());
        Assert.Equal(_sampleError.Code, exception.Code);
    }

    [Fact]
    public void Default_IsUninitializedFailure()
    {
        Result<string> result = default;

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.UninitializedResult, result.Error!.Code);
        Assert.False(result.TryGetValue(out _));
    }

    [Fact]
    public void Map_TransformsValueAndPropagatesError()
    {
        Result<int> success = 2;
        Result<int> failure = _sampleError;

        Assert.Equal("4", success.Map(static v => (v * 2).ToString(System.Globalization.CultureInfo.InvariantCulture)).ValueOrThrow());
        Assert.Same(_sampleError, failure.Map(static v => v * 2).Error);
    }

    [Fact]
    public void Bind_ChainsResults()
    {
        Result<int> success = 2;

        var chained = success.Bind(static v => v > 1 ? Result.Success("big") : Error.Validation("ORDERS_VALIDATION_001", "太小"));
        var failed = success.Bind(static v => v > 5 ? Result.Success("big") : Error.Validation("ORDERS_VALIDATION_001", "太小"));

        Assert.Equal("big", chained.ValueOrThrow());
        Assert.Equal("ORDERS_VALIDATION_001", failed.Error!.Code);
    }

    [Fact]
    public void Match_SelectsBranch()
    {
        Result<int> success = 1;
        Result<int> failure = _sampleError;

        Assert.Equal("1", success.Match(static v => v.ToString(System.Globalization.CultureInfo.InvariantCulture), static e => e.Code));
        Assert.Equal(_sampleError.Code, failure.Match(static v => v.ToString(System.Globalization.CultureInfo.InvariantCulture), static e => e.Code));
    }

    [Fact]
    public void ToResult_DropsValue()
    {
        Result<int> success = 1;
        Result<int> failure = _sampleError;

        Assert.True(success.ToResult().IsSuccess);
        Assert.Same(_sampleError, failure.ToResult().Error);
    }

    [Fact]
    public void Equality_ComparesValueAndError()
    {
        Assert.Equal(Result.Success(1), Result.Success(1));
        Assert.NotEqual(Result.Success(1), Result.Success(2));
        Assert.Equal(Result.Failure<int>(_sampleError), Result.Failure<int>(_sampleError));
    }
}
