// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Handlers;

namespace XiHan.Framework.Bot.Telegram.Tests.Handlers;

/// <summary>
/// <see cref="BotCallbackAttribute"/> 回调标记测试
/// </summary>
/// <remarks>
/// Action 与 callback data 的 <c>action:id</c> 约定强绑定：Action 若保留首尾空白，
/// 运行时从 callback data 里切出来的（已 Trim 的）Action 就永远匹配不上路由表。
/// </remarks>
public class BotCallbackAttributeTests
{
    /// <summary>
    /// 动作名为空时抛参数异常并指明参数名
    /// </summary>
    /// <param name="action">回调动作名</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenActionBlank_ThrowsArgumentException(string? action)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BotCallbackAttribute(action!));

        Assert.Equal("action", exception.ParamName);
        Assert.Contains("Action 不能为空", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 动作名去掉首尾空白后保存，大小写原样保留
    /// </summary>
    /// <param name="action">回调动作名</param>
    /// <param name="expected">保存结果</param>
    [Theory]
    [InlineData("confirm", "confirm")]
    [InlineData("  confirm  ", "confirm")]
    [InlineData("Confirm", "Confirm")]
    [InlineData("order.confirm", "order.confirm")]
    public void Constructor_TrimsAction(string action, string expected)
    {
        Assert.Equal(expected, new BotCallbackAttribute(action).Action);
    }

    /// <summary>
    /// 默认不是管理员限定，普通用户可点击
    /// </summary>
    [Fact]
    public void Defaults_AdminOnlyIsFalse()
    {
        Assert.False(new BotCallbackAttribute("confirm").AdminOnly);
    }

    /// <summary>
    /// 管理员限定标记可显式开启
    /// </summary>
    [Fact]
    public void AdminOnly_CanBeEnabled()
    {
        var attribute = new BotCallbackAttribute("purge")
        {
            AdminOnly = true
        };

        Assert.True(attribute.AdminOnly);
        Assert.Equal("purge", attribute.Action);
    }

    /// <summary>
    /// 属性允许在同一个类上标注多次，且不被子类继承
    /// </summary>
    [Fact]
    public void AttributeUsage_AllowsMultipleOnClassAndIsNotInherited()
    {
        var usage = typeof(BotCallbackAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.True(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }
}
