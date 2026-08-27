// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Core;

namespace XiHan.Framework.Bot.Telegram.Tests.Core;

/// <summary>
/// <see cref="TelegramCommandGuards"/> 命令放行规则测试
/// </summary>
/// <remarks>
/// 永久放行清单只豁免「群组/频道白名单守卫」这一条，命令白名单与 AdminOnly 仍然生效——
/// 这是 fail-closed 设计的最后一道缝，把它测严实很重要：
/// 放宽了会让任意群组都能用机器人，收紧了会让用户在未授权群里连 /start 都调不出来。
/// </remarks>
public class TelegramCommandGuardsTests
{
    /// <summary>
    /// 空输入归一化为 null
    /// </summary>
    /// <param name="commandToken">命令 token</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeCommandToken_WhenBlank_ReturnsNull(string? commandToken)
    {
        Assert.Null(TelegramCommandGuards.NormalizeCommandToken(commandToken));
    }

    /// <summary>
    /// 归一化补齐前导斜杠、去掉 @bot 后缀与首尾空白，但保留原有大小写
    /// </summary>
    /// <param name="commandToken">命令 token</param>
    /// <param name="expected">归一化结果</param>
    [Theory]
    [InlineData("/start", "/start")]
    [InlineData("start", "/start")]
    [InlineData("  /start  ", "/start")]
    [InlineData("/start@my_bot", "/start")]
    [InlineData("start@my_bot", "/start")]
    [InlineData("/Start", "/Start")]
    [InlineData("/order@MyBot", "/order")]
    public void NormalizeCommandToken_NormalizesSlashAndBotSuffix(string commandToken, string expected)
    {
        Assert.Equal(expected, TelegramCommandGuards.NormalizeCommandToken(commandToken));
    }

    /// <summary>
    /// 永久放行清单覆盖 /start、/myid、/id、/help、/h 五个 token
    /// </summary>
    /// <param name="commandToken">命令 token</param>
    [Theory]
    [InlineData("/start")]
    [InlineData("/myid")]
    [InlineData("/id")]
    [InlineData("/help")]
    [InlineData("/h")]
    public void IsAlwaysAvailableCommandToken_ForBuiltinCommands_ReturnsTrue(string commandToken)
    {
        Assert.True(TelegramCommandGuards.IsAlwaysAvailableCommandToken(commandToken));
    }

    /// <summary>
    /// 永久放行判定不区分大小写、允许省略斜杠、允许携带 @bot 后缀
    /// </summary>
    /// <param name="commandToken">命令 token</param>
    [Theory]
    [InlineData("start")]
    [InlineData("/START")]
    [InlineData("/Start@my_bot")]
    [InlineData("  /help  ")]
    [InlineData("HELP")]
    public void IsAlwaysAvailableCommandToken_IgnoresCaseSlashAndBotSuffix(string commandToken)
    {
        Assert.True(TelegramCommandGuards.IsAlwaysAvailableCommandToken(commandToken));
    }

    /// <summary>
    /// 业务命令与空输入均不在永久放行清单
    /// </summary>
    /// <param name="commandToken">命令 token</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/order")]
    [InlineData("/ban")]
    [InlineData("/startup")]
    [InlineData("/helper")]
    public void IsAlwaysAvailableCommandToken_ForOtherTokens_ReturnsFalse(string? commandToken)
    {
        Assert.False(TelegramCommandGuards.IsAlwaysAvailableCommandToken(commandToken));
    }

    /// <summary>
    /// 路由绑定的命令集合为 null 或空时不放行
    /// </summary>
    [Fact]
    public void IsAlwaysAvailableRoute_WhenNullOrEmpty_ReturnsFalse()
    {
        Assert.False(TelegramCommandGuards.IsAlwaysAvailableRoute(null));
        Assert.False(TelegramCommandGuards.IsAlwaysAvailableRoute([]));
    }

    /// <summary>
    /// 路由绑定的命令中只要有一个在清单内即整体放行（主命令或别名命中都算）
    /// </summary>
    [Fact]
    public void IsAlwaysAvailableRoute_WhenAnyCommandMatches_ReturnsTrue()
    {
        Assert.True(TelegramCommandGuards.IsAlwaysAvailableRoute(["/order", "/help"]));
        Assert.True(TelegramCommandGuards.IsAlwaysAvailableRoute(["/h"]));
    }

    /// <summary>
    /// 路由绑定的命令全部不在清单内则不放行
    /// </summary>
    [Fact]
    public void IsAlwaysAvailableRoute_WhenNoCommandMatches_ReturnsFalse()
    {
        Assert.False(TelegramCommandGuards.IsAlwaysAvailableRoute(["/order", "/o", "/ban"]));
    }
}
