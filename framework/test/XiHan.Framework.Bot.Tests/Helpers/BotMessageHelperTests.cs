// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Tests.Helpers;

/// <summary>
/// <see cref="BotMessageHelper"/> 测试
/// </summary>
/// <remarks>
/// 所有提供者子包都靠这一个方法从消息 Data 里取自己的扩展参数，
/// 所以"类型不匹配时不抛异常、只返回 false"是必须保证的契约，否则一条脏配置会打断整条调度链。
/// </remarks>
public class BotMessageHelperTests
{
    /// <summary>
    /// 键存在且类型匹配时取值成功
    /// </summary>
    [Fact]
    public void TryGetData_WhenKeyExistsAndTypeMatches_ReturnsTrue()
    {
        var message = new BotMessage();
        message.Data[BotMessageDataKeys.Strategy] = BotStrategyNames.Failover;

        var found = BotMessageHelper.TryGetData<string>(message, BotMessageDataKeys.Strategy, out var value);

        Assert.True(found);
        Assert.Equal(BotStrategyNames.Failover, value);
    }

    /// <summary>
    /// 键名大小写不敏感
    /// </summary>
    [Fact]
    public void TryGetData_KeyLookupIsCaseInsensitive()
    {
        var message = new BotMessage();
        message.Data["strategy"] = "Priority";

        var found = BotMessageHelper.TryGetData<string>(message, "STRATEGY", out var value);

        Assert.True(found);
        Assert.Equal("Priority", value);
    }

    /// <summary>
    /// 键不存在时返回 false 且输出默认值
    /// </summary>
    [Fact]
    public void TryGetData_WhenKeyMissing_ReturnsFalse()
    {
        var message = new BotMessage();

        var found = BotMessageHelper.TryGetData<string>(message, "NotExists", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    /// <summary>
    /// 类型不匹配时返回 false 而不是抛出转换异常
    /// </summary>
    [Fact]
    public void TryGetData_WhenTypeMismatch_ReturnsFalse()
    {
        var message = new BotMessage();
        message.Data["Retry"] = "3";

        var found = BotMessageHelper.TryGetData<int>(message, "Retry", out var value);

        Assert.False(found);
        Assert.Equal(0, value);
    }

    /// <summary>
    /// 值为 null 时按取不到处理
    /// </summary>
    [Fact]
    public void TryGetData_WhenValueNull_ReturnsFalse()
    {
        var message = new BotMessage();
        message.Data["Strategy"] = null;

        var found = BotMessageHelper.TryGetData<string>(message, "Strategy", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    /// <summary>
    /// Data 字典整体为 null 时不抛空引用
    /// </summary>
    [Fact]
    public void TryGetData_WhenDataNull_ReturnsFalse()
    {
        var message = new BotMessage { Data = null! };

        var found = BotMessageHelper.TryGetData<string>(message, "Strategy", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    /// <summary>
    /// 值类型取值命中时返回原值
    /// </summary>
    [Fact]
    public void TryGetData_WithValueType_ReturnsBoxedValue()
    {
        var message = new BotMessage();
        message.Data["Retry"] = 3;

        var found = BotMessageHelper.TryGetData<int>(message, "Retry", out var value);

        Assert.True(found);
        Assert.Equal(3, value);
    }
}
