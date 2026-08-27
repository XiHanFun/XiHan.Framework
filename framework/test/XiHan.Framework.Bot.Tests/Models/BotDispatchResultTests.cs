// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotDispatchResult"/> 测试
/// </summary>
/// <remarks>
/// 该类型的文档契约是"至少有一个提供者结果且全部成功才算成功"，调用方据此做 fail-closed 判定，
/// 所以空结果、被跳过、部分失败三种情况都必须落在 IsSuccess=false 上。
/// </remarks>
public class BotDispatchResultTests
{
    /// <summary>
    /// 无提供者结果不成功且带错误说明
    /// </summary>
    [Fact]
    public void NoProvider_IsFailureWithMessage()
    {
        var result = BotDispatchResult.NoProvider("No bot provider configured.");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsSkipped);
        Assert.Equal("No bot provider configured.", result.ErrorMessage);
        Assert.Empty(result.Results);
    }

    /// <summary>
    /// 全部成功时聚合为成功且不带错误说明
    /// </summary>
    [Fact]
    public void From_WhenAllSucceeded_IsSuccess()
    {
        var results = new[]
        {
            BotResult.Success(provider: "A"),
            BotResult.Success(provider: "B")
        };

        var dispatch = BotDispatchResult.From(results, false);

        Assert.True(dispatch.IsSuccess);
        Assert.False(dispatch.IsSkipped);
        Assert.Null(dispatch.ErrorMessage);
        Assert.Equal(2, dispatch.Results.Count);
    }

    /// <summary>
    /// 存在失败时聚合为失败并把各失败拼进错误说明
    /// </summary>
    [Fact]
    public void From_WhenAnyFailed_AggregatesErrorMessages()
    {
        var results = new[]
        {
            BotResult.Success(provider: "A"),
            BotResult.Failed("timeout", "B"),
            BotResult.BadRequest("bad payload", "C")
        };

        var dispatch = BotDispatchResult.From(results, false);

        Assert.False(dispatch.IsSuccess);
        Assert.NotNull(dispatch.ErrorMessage);
        Assert.Contains("B:timeout", dispatch.ErrorMessage!);
        Assert.Contains("C:bad payload", dispatch.ErrorMessage!);
        Assert.DoesNotContain("A:", dispatch.ErrorMessage!);
        Assert.Equal("B:timeout；C:bad payload", dispatch.ErrorMessage);
    }

    /// <summary>
    /// 结果为空时不成功并给出专属说明
    /// </summary>
    [Fact]
    public void From_WhenEmpty_IsFailureWithNoResultMessage()
    {
        var dispatch = BotDispatchResult.From(Array.Empty<BotResult>(), false);

        Assert.False(dispatch.IsSuccess);
        Assert.Equal("Bot dispatch finished with no results.", dispatch.ErrorMessage);
        Assert.Empty(dispatch.Results);
    }

    /// <summary>
    /// 被跳过时即便全部成功也不算成功
    /// </summary>
    [Fact]
    public void From_WhenSkipped_IsNotSuccessEvenWithSuccessResults()
    {
        var results = new[] { BotResult.Success(provider: "A") };

        var dispatch = BotDispatchResult.From(results, true);

        Assert.False(dispatch.IsSuccess);
        Assert.True(dispatch.IsSkipped);
        Assert.Equal("Bot dispatch skipped.", dispatch.ErrorMessage);
        Assert.Single(dispatch.Results);
    }

    /// <summary>
    /// 结果列表为 null 时按空结果处理而不抛出
    /// </summary>
    [Fact]
    public void From_WhenResultsNull_TreatsAsEmpty()
    {
        var dispatch = BotDispatchResult.From(null!, false);

        Assert.False(dispatch.IsSuccess);
        Assert.Empty(dispatch.Results);
        Assert.Equal("Bot dispatch finished with no results.", dispatch.ErrorMessage);
    }

    /// <summary>
    /// 聚合结果对入参做快照，后续改动源列表不影响已返回的结果
    /// </summary>
    [Fact]
    public void From_TakesSnapshotOfResults()
    {
        var results = new List<BotResult> { BotResult.Success(provider: "A") };

        var dispatch = BotDispatchResult.From(results, false);
        results.Add(BotResult.Failed("late", "B"));

        Assert.Single(dispatch.Results);
        Assert.True(dispatch.IsSuccess);
    }

    /// <summary>
    /// 默认实例的结果列表非 null
    /// </summary>
    [Fact]
    public void Defaults_ResultsIsEmptyNotNull()
    {
        var dispatch = new BotDispatchResult();

        Assert.NotNull(dispatch.Results);
        Assert.Empty(dispatch.Results);
        Assert.False(dispatch.IsSuccess);
        Assert.False(dispatch.IsSkipped);
        Assert.Null(dispatch.ErrorMessage);
    }
}
