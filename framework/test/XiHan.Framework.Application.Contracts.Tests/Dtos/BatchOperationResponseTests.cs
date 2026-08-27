// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// 批量操作响应测试
/// </summary>
/// <remarks>
/// 该响应有一条容易踩的语义：<c>IsAllSuccess</c> 只看 <c>FailureCount == 0</c>，
/// 既不比对 <c>SuccessCount</c>，也不比对 <c>TotalCount</c>。
/// 也就是说「全部被跳过」同样会被判为全成功，调用方若要严格判定必须自己比对计数。
/// 这里把该语义显式锁下来，避免有人凭直觉当成 SuccessCount == TotalCount。
/// </remarks>
public class BatchOperationResponseTests
{
    /// <summary>
    /// 默认响应：计数全零、集合非空且为空、判定为全成功
    /// </summary>
    [Fact]
    public void Defaults_AreZeroCountsAndEmptyCollections()
    {
        var response = new BatchOperationResponse<string>();

        Assert.Equal(0, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
        Assert.Equal(0, response.TotalCount);
        Assert.NotNull(response.Results);
        Assert.Empty(response.Results);
        Assert.NotNull(response.Errors);
        Assert.Empty(response.Errors);
        Assert.True(response.IsAllSuccess);
    }

    /// <summary>
    /// 全成功判定只取决于失败数，与成功数、总数无关
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(5, 0, 5, true)]
    [InlineData(0, 0, 5, true)]
    [InlineData(4, 1, 5, false)]
    [InlineData(0, 5, 5, false)]
    public void IsAllSuccess_DependsOnlyOnFailureCount(int successCount, int failureCount, int totalCount, bool expected)
    {
        var response = new BatchOperationResponse<string>
        {
            SuccessCount = successCount,
            FailureCount = failureCount,
            TotalCount = totalCount
        };

        Assert.Equal(expected, response.IsAllSuccess);
    }

    /// <summary>
    /// 单条结果默认：索引 0、判定失败、无数据无错误码
    /// </summary>
    /// <remarks>
    /// <c>IsSuccess</c> 默认 false 是安全侧默认——忘记赋值时不会把失败项当成功项。
    /// </remarks>
    [Fact]
    public void BatchOperationResult_Defaults_AreFailureShaped()
    {
        var result = new BatchOperationResult<string>();

        Assert.Equal(0, result.Index);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ErrorCode);
    }

    /// <summary>
    /// 值类型结果的数据默认值为类型默认值
    /// </summary>
    [Fact]
    public void BatchOperationResult_WithValueType_DataDefaultsToTypeDefault()
    {
        var result = new BatchOperationResult<int>();

        Assert.Equal(0, result.Data);
    }

    /// <summary>
    /// 结果项按索引定位原始入参位置，索引不会被响应自动维护
    /// </summary>
    [Fact]
    public void Results_CarryIndexBackToRequestItems()
    {
        var response = new BatchOperationResponse<string>
        {
            TotalCount = 2,
            SuccessCount = 1,
            FailureCount = 1,
            Results =
            [
                new BatchOperationResult<string> { Index = 0, IsSuccess = true, Data = "ok" },
                new BatchOperationResult<string> { Index = 1, IsSuccess = false, ErrorCode = "E001", ErrorMessage = "名称重复" }
            ],
            Errors = ["第 1 项名称重复"]
        };

        Assert.False(response.IsAllSuccess);
        Assert.Equal(2, response.Results.Count);
        Assert.Equal(0, response.Results[0].Index);
        Assert.Equal("ok", response.Results[0].Data);
        Assert.Equal(1, response.Results[1].Index);
        Assert.Equal("E001", response.Results[1].ErrorCode);
        Assert.Contains("第 1 项名称重复", response.Errors);
    }

    /// <summary>
    /// 序列化字段名锁定，且往返保值（只读的 IsAllSuccess 由 FailureCount 重新推导）
    /// </summary>
    [Fact]
    public void RoundTrip_PreservesCountsAndResults()
    {
        var response = new BatchOperationResponse<int>
        {
            TotalCount = 3,
            SuccessCount = 2,
            FailureCount = 1,
            Results =
            [
                new BatchOperationResult<int> { Index = 0, IsSuccess = true, Data = 11 },
                new BatchOperationResult<int> { Index = 1, IsSuccess = true, Data = 22 },
                new BatchOperationResult<int> { Index = 2, IsSuccess = false, ErrorCode = "E002" }
            ],
            Errors = ["第 2 项失败"]
        };

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"SuccessCount\":2", json);
        Assert.Contains("\"FailureCount\":1", json);
        Assert.Contains("\"TotalCount\":3", json);
        Assert.Contains("\"IsAllSuccess\":false", json);
        Assert.Contains("\"Results\":[", json);
        Assert.Contains("\"Errors\":[", json);

        var restored = JsonSerializer.Deserialize<BatchOperationResponse<int>>(json);

        Assert.NotNull(restored);
        Assert.Equal(3, restored!.TotalCount);
        Assert.Equal(2, restored.SuccessCount);
        Assert.Equal(1, restored.FailureCount);
        Assert.False(restored.IsAllSuccess);
        Assert.Equal(3, restored.Results.Count);
        Assert.Equal(11, restored.Results[0].Data);
        Assert.Equal("E002", restored.Results[2].ErrorCode);
        Assert.Single(restored.Errors);
    }
}
