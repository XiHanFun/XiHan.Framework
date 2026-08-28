// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Crons;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Crons;

/// <summary>
/// CronExpression 表达式对象测试
/// </summary>
/// <remarks>
/// 重点是"解析 → 字符串化 → 再解析"的闭环稳定：调度器把表达式落库、回读再排期时依赖这一点。
/// </remarks>
public class CronExpressionTests
{
    /// <summary>
    /// 新建对象的各字段都已初始化，不需要调用方补 null 检查
    /// </summary>
    [Fact]
    public void Constructor_Default_InitializesAllFields()
    {
        var expression = new CronExpression();

        Assert.False(expression.HasSeconds);
        Assert.NotNull(expression.Seconds);
        Assert.NotNull(expression.Minutes);
        Assert.NotNull(expression.Hours);
        Assert.NotNull(expression.Days);
        Assert.NotNull(expression.Months);
        Assert.NotNull(expression.DaysOfWeek);
    }

    /// <summary>
    /// 5 位表达式字符串化后保持 5 段
    /// </summary>
    [Theory]
    [InlineData("* * * * *", "* * * * *")]
    [InlineData("0 2 * * *", "0 2 * * *")]
    [InlineData("0 9-11 * * *", "0 9,10,11 * * *")]
    [InlineData("*/30 * * * *", "0,30 * * * *")]
    [InlineData("? * * * *", "* * * * *")]
    public void ToString_WithFivePartExpression_KeepsFiveSegments(string source, string expected)
    {
        var expression = CronHelper.ParseExpression(source);

        Assert.Equal(expected, expression.ToString());
    }

    /// <summary>
    /// 6 位表达式字符串化后保持 6 段并把秒放在首位
    /// </summary>
    [Theory]
    [InlineData("0 0 12 * * *", "0 0 12 * * *")]
    [InlineData("*/20 * * * * *", "0,20,40 * * * * *")]
    public void ToString_WithSixPartExpression_KeepsSixSegments(string source, string expected)
    {
        var expression = CronHelper.ParseExpression(source);

        Assert.True(expression.HasSeconds);
        Assert.Equal(expected, expression.ToString());
    }

    /// <summary>
    /// 字符串化的结果能被重新解析，且再次字符串化保持不变（幂等闭环）
    /// </summary>
    [Theory]
    [InlineData("0 2 * * *")]
    [InlineData("*/5 * * * *")]
    [InlineData("0 9-17 * * 1-5")]
    [InlineData("0 0 12 * * *")]
    public void ToString_RoundTrip_IsIdempotent(string source)
    {
        var first = CronHelper.ParseExpression(source).ToString();
        var second = CronHelper.ParseExpression(first).ToString();

        Assert.Equal(first, second);
        Assert.True(CronHelper.IsValidExpression(first));
    }

    /// <summary>
    /// 字符串化的结果与直接格式化原表达式一致
    /// </summary>
    [Fact]
    public void ToString_MatchesFormatExpressionOfSameSource()
    {
        const string Source = "0 9-11 * * *";

        Assert.Equal(CronHelper.FormatExpression(Source), CronHelper.ParseExpression(Source).ToString());
    }

    /// <summary>
    /// 手工组装的表达式对象同样能字符串化为可解析的表达式
    /// </summary>
    [Fact]
    public void ToString_WithManuallyComposedFields_ProducesParsableExpression()
    {
        var expression = new CronExpression
        {
            HasSeconds = false,
            Minutes = new CronField { Values = [0] },
            Hours = new CronField { Values = [3] },
            Days = new CronField { IsWildcard = true },
            Months = new CronField { IsWildcard = true },
            DaysOfWeek = new CronField { IsWildcard = true }
        };

        var text = expression.ToString();

        Assert.Equal("0 3 * * *", text);
        Assert.True(CronHelper.IsValidExpression(text));
    }
}
