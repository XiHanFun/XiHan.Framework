// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using XiHan.Framework.Bot.Telegram.Handlers;

namespace XiHan.Framework.Bot.Telegram.Tests.Handlers;

/// <summary>
/// <see cref="BotCommandAttribute"/> 命令标记测试
/// </summary>
/// <remarks>
/// 这个属性是命令路由表的唯一数据源：命令归一化错了会整条命令失联，
/// 别名去重错了会在建目录时误报「命令重复」而让整个应用启动失败，
/// 正则少了超时会被一条精心构造的消息 ReDoS 掉整个分发线程。三条都要锁住。
/// </remarks>
public class BotCommandAttributeTests
{
    /// <summary>
    /// 命令为空时抛参数异常并指明参数名
    /// </summary>
    /// <param name="command">命令文本</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenCommandBlank_ThrowsArgumentException(string? command)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BotCommandAttribute(command!));

        Assert.Equal("command", exception.ParamName);
        Assert.Contains("Command 不能为空", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 命令统一归一为带前导斜杠，并去掉首尾空白
    /// </summary>
    /// <param name="command">命令文本</param>
    /// <param name="expected">归一化结果</param>
    [Theory]
    [InlineData("/order", "/order")]
    [InlineData("order", "/order")]
    [InlineData("  order  ", "/order")]
    [InlineData("  /order  ", "/order")]
    [InlineData("Order", "/Order")]
    public void Constructor_NormalizesLeadingSlash(string command, string expected)
    {
        Assert.Equal(expected, new BotCommandAttribute(command).Command);
    }

    /// <summary>
    /// 未显式设置时描述为空串、非管理员限定、无别名、无正则
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyDescriptionPublicCommandNoAliasNoPattern()
    {
        var attribute = new BotCommandAttribute("/order");

        Assert.Equal(string.Empty, attribute.Description);
        Assert.False(attribute.AdminOnly);
        Assert.Empty(attribute.Aliases);
        Assert.Null(attribute.Pattern);
        Assert.Null(attribute.BuildRegex());
    }

    /// <summary>
    /// 别名同样归一为带斜杠，并按忽略大小写去重、剔除空白项
    /// </summary>
    [Fact]
    public void GetNormalizedAliases_NormalizesDeduplicatesAndDropsBlanks()
    {
        var attribute = new BotCommandAttribute("/order")
        {
            Aliases = ["o", "/o", "/O", string.Empty, "   ", "od"]
        };

        var aliases = attribute.GetNormalizedAliases();

        Assert.Equal(2, aliases.Length);
        Assert.Equal("/o", aliases[0]);
        Assert.Equal("/od", aliases[1]);
    }

    /// <summary>
    /// 别名为 null 时返回空数组而不是抛空引用
    /// </summary>
    [Fact]
    public void GetNormalizedAliases_WhenAliasesNull_ReturnsEmpty()
    {
        var attribute = new BotCommandAttribute("/order")
        {
            Aliases = null!
        };

        Assert.Empty(attribute.GetNormalizedAliases());
    }

    /// <summary>
    /// 全部为空白的别名列表归一化后为空
    /// </summary>
    [Fact]
    public void GetNormalizedAliases_WhenAllBlank_ReturnsEmpty()
    {
        var attribute = new BotCommandAttribute("/order")
        {
            Aliases = [string.Empty, "   "]
        };

        Assert.Empty(attribute.GetNormalizedAliases());
    }

    /// <summary>
    /// 正则未配置或全为空白时不构建正则
    /// </summary>
    /// <param name="pattern">正则文本</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildRegex_WhenPatternBlank_ReturnsNull(string? pattern)
    {
        var attribute = new BotCommandAttribute("/order")
        {
            Pattern = pattern
        };

        Assert.Null(attribute.BuildRegex());
    }

    /// <summary>
    /// 构建的正则带 100ms 匹配超时，防止恶意文本触发 ReDoS 拖垮分发
    /// </summary>
    [Fact]
    public void BuildRegex_AppliesHundredMillisecondTimeout()
    {
        var attribute = new BotCommandAttribute("/query")
        {
            Pattern = @"^查单\s+(\d+)$"
        };

        var regex = attribute.BuildRegex();

        Assert.NotNull(regex);
        Assert.Equal(TimeSpan.FromMilliseconds(100), regex!.MatchTimeout);
    }

    /// <summary>
    /// 构建的正则启用编译与文化不变，避免运行环境文化影响匹配结果
    /// </summary>
    [Fact]
    public void BuildRegex_UsesCompiledAndCultureInvariantOptions()
    {
        var attribute = new BotCommandAttribute("/query")
        {
            Pattern = "^abc$"
        };

        var regex = attribute.BuildRegex();

        Assert.NotNull(regex);
        Assert.True(regex!.Options.HasFlag(RegexOptions.Compiled));
        Assert.True(regex.Options.HasFlag(RegexOptions.CultureInvariant));
    }

    /// <summary>
    /// 构建的正则按配置文本工作，且首尾空白会被裁剪
    /// </summary>
    [Fact]
    public void BuildRegex_TrimsPatternAndMatchesAsConfigured()
    {
        var attribute = new BotCommandAttribute("/query")
        {
            Pattern = "  ^查单\\s+(\\d+)$  "
        };

        var regex = attribute.BuildRegex();

        Assert.NotNull(regex);
        var match = regex!.Match("查单 12345");
        Assert.True(match.Success);
        Assert.Equal("12345", match.Groups[1].Value);
        Assert.False(regex.IsMatch("查单"));
    }

    /// <summary>
    /// 每次调用都返回新的正则实例，属性本身不缓存状态
    /// </summary>
    [Fact]
    public void BuildRegex_ReturnsNewInstanceEachCall()
    {
        var attribute = new BotCommandAttribute("/query")
        {
            Pattern = "^abc$"
        };

        Assert.NotSame(attribute.BuildRegex(), attribute.BuildRegex());
    }

    /// <summary>
    /// 属性允许在同一个类上标注多次，且不被子类继承
    /// </summary>
    /// <remarks>
    /// 允许多次是为了一个处理器绑定多个命令；不继承是为了避免子类静默继承父类命令导致「命令重复」启动失败。
    /// </remarks>
    [Fact]
    public void AttributeUsage_AllowsMultipleOnClassAndIsNotInherited()
    {
        var usage = typeof(BotCommandAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.True(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }
}
