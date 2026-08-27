// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Crons;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Crons;

/// <summary>
/// CronExpression 诊断路径（ToString）健壮性测试
/// </summary>
/// <remarks>
/// ToString 会被日志、调试器、异常消息随手调到，任何状态下都不能抛异常。原实现把拼好的串再交给
/// CronHelper.FormatExpression 解析一遍，于是"字段还没填"的对象一字符串化就抛 ArgumentException。
/// 这里覆盖默认对象、部分填充对象与带秒对象三种未完成状态，并确认合法解析结果的输出口径没有变。
/// </remarks>
public class CronExpressionDiagnosticsTests
{
    /// <summary>
    /// 默认构造的对象字符串化不抛异常，且仍然是 5 段
    /// </summary>
    [Fact]
    public void ToString_OnDefaultInstance_DoesNotThrowAndKeepsFiveSegments()
    {
        var expression = new CronExpression();

        var text = expression.ToString();

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Equal(5, text.Split(' ').Length);
    }

    /// <summary>
    /// 带秒的未完成对象字符串化为 6 段，同样不抛异常
    /// </summary>
    [Fact]
    public void ToString_OnDefaultInstanceWithSeconds_KeepsSixSegments()
    {
        var expression = new CronExpression { HasSeconds = true };

        Assert.Equal(6, expression.ToString().Split(' ').Length);
    }

    /// <summary>
    /// 未完成对象的输出不会被误判成合法表达式（占位符不能是 * 那种"每一刻都执行"的语义）
    /// </summary>
    [Fact]
    public void ToString_OnDefaultInstance_IsNotMistakenForValidExpression()
    {
        var text = new CronExpression().ToString();

        Assert.DoesNotContain("*", text, StringComparison.Ordinal);
        Assert.False(CronHelper.IsValidExpression(text));
    }

    /// <summary>
    /// 只填了一部分字段时照样能字符串化，已填字段原样呈现
    /// </summary>
    [Fact]
    public void ToString_WithPartiallyFilledFields_RendersFilledFieldsAndPlaceholders()
    {
        var expression = new CronExpression
        {
            Minutes = new CronField { Values = [0] },
            Hours = new CronField { IsWildcard = true }
        };

        var text = expression.ToString();

        Assert.StartsWith("0 * ", text, StringComparison.Ordinal);
        Assert.Equal(5, text.Split(' ').Length);
    }

    /// <summary>
    /// 手工把某一段清空后字符串化仍然安全，不会因为"少了一段"而抛异常
    /// </summary>
    [Fact]
    public void ToString_WhenOneFieldIsCleared_StillDoesNotThrow()
    {
        var expression = CronHelper.ParseExpression("0 2 * * *");
        expression.Minutes = new CronField();

        var text = expression.ToString();

        Assert.Equal(5, text.Split(' ').Length);
        Assert.EndsWith("2 * * *", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 合法解析结果的字符串化口径不变：与 CronHelper.FormatExpression 完全一致
    /// </summary>
    [Theory]
    [InlineData("0 9-11 * * *")]
    [InlineData("*/30 * * * *")]
    [InlineData("? * * * *")]
    [InlineData("0 0 12 * * *")]
    [InlineData("0/15 * * * * *")]
    public void ToString_OnParsedExpression_MatchesFormatExpression(string source)
    {
        var expression = CronHelper.ParseExpression(source);

        Assert.Equal(CronHelper.FormatExpression(source), expression.ToString());
    }

    /// <summary>
    /// 字符串化结果能被重新解析，且再次字符串化保持不变
    /// </summary>
    [Theory]
    [InlineData("0 0 1 * 1")]
    [InlineData("0/15 * * * * *")]
    public void ToString_RoundTrip_StaysStable(string source)
    {
        var first = CronHelper.ParseExpression(source).ToString();
        var second = CronHelper.ParseExpression(first).ToString();

        Assert.Equal(first, second);
        Assert.True(CronHelper.IsValidExpression(first));
    }
}
