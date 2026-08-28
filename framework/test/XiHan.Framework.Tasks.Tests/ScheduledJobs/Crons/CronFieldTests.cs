// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Crons;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Crons;

/// <summary>
/// CronField 字段对象测试
/// </summary>
/// <remarks>
/// CronField 是 Cron 解析结果的最小载体，它的默认值语义与字符串化形式会被 CronExpression、
/// CronHelper.FormatExpression 直接复用，所以这里把默认值和 ToString 的口径钉死。
/// </remarks>
public class CronFieldTests
{
    /// <summary>
    /// 新建字段默认既不是通配也没有取值
    /// </summary>
    [Fact]
    public void Constructor_Default_HasNoWildcardAndEmptyValues()
    {
        var field = new CronField();

        Assert.False(field.IsWildcard);
        Assert.NotNull(field.Values);
        Assert.Empty(field.Values);
    }

    /// <summary>
    /// 两个字段实例各自持有独立的取值集合，互不串扰
    /// </summary>
    [Fact]
    public void Values_OnDifferentInstances_AreNotShared()
    {
        var first = new CronField();
        var second = new CronField();

        first.Values.Add(7);

        Assert.Empty(second.Values);
    }

    /// <summary>
    /// 通配字段无论有没有取值都字符串化为星号
    /// </summary>
    [Fact]
    public void ToString_WhenWildcard_ReturnsAsterisk()
    {
        var field = new CronField { IsWildcard = true, Values = [1, 2, 3] };

        Assert.Equal("*", field.ToString());
    }

    /// <summary>
    /// 非通配字段字符串化为逗号分隔的取值列表，顺序沿用集合顺序
    /// </summary>
    [Fact]
    public void ToString_WithValues_JoinsWithComma()
    {
        var field = new CronField { Values = [0, 15, 30, 45] };

        Assert.Equal("0,15,30,45", field.ToString());
    }

    /// <summary>
    /// 单值字段不带多余分隔符
    /// </summary>
    [Fact]
    public void ToString_WithSingleValue_HasNoSeparator()
    {
        var field = new CronField { Values = [9] };

        Assert.Equal("9", field.ToString());
    }
}
