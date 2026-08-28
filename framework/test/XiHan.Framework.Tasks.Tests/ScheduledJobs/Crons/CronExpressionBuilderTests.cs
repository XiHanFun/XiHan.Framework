// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Crons;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Crons;

/// <summary>
/// CronExpressionBuilder 表达式构建器测试
/// </summary>
/// <remarks>
/// 构建器只负责按位拼装，不做校验；这里同时验证"拼出来的东西 CronHelper 一定能解析"这条隐式契约。
/// </remarks>
public class CronExpressionBuilderTests
{
    /// <summary>
    /// 未设置任何字段时构建出全通配的 5 位表达式
    /// </summary>
    [Fact]
    public void Build_WithoutAnySetter_ReturnsAllWildcardFivePartExpression()
    {
        var expression = CronExpressionBuilder.Create().Build();

        Assert.Equal("* * * * *", expression);
        Assert.True(CronHelper.IsValidExpression(expression));
    }

    /// <summary>
    /// 设置分时字段后按"分 时 日 月 周"顺序拼装
    /// </summary>
    [Fact]
    public void Build_WithMinutesAndHours_KeepsFieldOrder()
    {
        var expression = CronExpressionBuilder.Create()
            .Minutes("30")
            .Hours("2")
            .Build();

        Assert.Equal("30 2 * * *", expression);
    }

    /// <summary>
    /// 五个字段全部设置时逐位落到正确的段
    /// </summary>
    [Fact]
    public void Build_WithAllFivePartFields_MapsEachFieldToItsSegment()
    {
        var expression = CronExpressionBuilder.Create()
            .Minutes("0")
            .Hours("9-17")
            .Days("1,15")
            .Months("1-6")
            .DaysOfWeek("1-5")
            .Build();

        Assert.Equal("0 9-17 1,15 1-6 1-5", expression);
        Assert.True(CronHelper.IsValidExpression(expression));
    }

    /// <summary>
    /// 一旦设置秒字段就切换为 6 位表达式，秒在首位
    /// </summary>
    [Fact]
    public void Build_WhenSecondsConfigured_SwitchesToSixPartExpression()
    {
        var expression = CronExpressionBuilder.Create()
            .Seconds("*/15")
            .Minutes("0")
            .Hours("3")
            .Build();

        Assert.Equal(6, expression.Split(' ').Length);
        Assert.Equal("*/15 0 3 * * *", expression);
        Assert.True(CronHelper.IsValidExpression(expression));
    }

    /// <summary>
    /// 每个设置方法都返回同一个构建器实例，保证链式调用不丢状态
    /// </summary>
    [Fact]
    public void Setters_ReturnSameBuilderInstance()
    {
        var builder = CronExpressionBuilder.Create();

        Assert.Same(builder, builder.Seconds("0"));
        Assert.Same(builder, builder.Minutes("0"));
        Assert.Same(builder, builder.Hours("0"));
        Assert.Same(builder, builder.Days("*"));
        Assert.Same(builder, builder.Months("*"));
        Assert.Same(builder, builder.DaysOfWeek("*"));
    }

    /// <summary>
    /// 对同一字段重复设置时后者覆盖前者
    /// </summary>
    [Fact]
    public void Setters_WhenCalledTwice_LastValueWins()
    {
        var expression = CronExpressionBuilder.Create()
            .Hours("1")
            .Hours("2")
            .Build();

        Assert.Equal("* 2 * * *", expression);
    }

    /// <summary>
    /// 多次 Build 不消耗构建器状态，可重复取值
    /// </summary>
    [Fact]
    public void Build_CalledTwice_ReturnsSameExpression()
    {
        var builder = CronExpressionBuilder.Create().Minutes("5");

        Assert.Equal(builder.Build(), builder.Build());
    }

    /// <summary>
    /// Create 每次返回全新实例，不同构建器之间互不影响
    /// </summary>
    [Fact]
    public void Create_ReturnsIndependentInstances()
    {
        var first = CronExpressionBuilder.Create().Minutes("10");
        var second = CronExpressionBuilder.Create();

        Assert.NotSame(first, second);
        Assert.Equal("* * * * *", second.Build());
    }

    /// <summary>
    /// 构建器不做校验，非法字段原样输出，由 CronHelper 在解析时拦截
    /// </summary>
    [Fact]
    public void Build_WithInvalidFieldValue_DoesNotValidateButHelperRejects()
    {
        var expression = CronExpressionBuilder.Create().Minutes("99").Build();

        Assert.Equal("99 * * * *", expression);
        Assert.False(CronHelper.IsValidExpression(expression));
    }
}
