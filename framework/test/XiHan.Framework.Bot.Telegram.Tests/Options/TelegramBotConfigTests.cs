// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramBotConfig"/> 单机器人配置测试
/// </summary>
/// <remarks>
/// <see cref="TelegramBotConfig.IsSameAs"/> 是管理器判定「要不要重启这个机器人」的唯一依据：
/// 判重过松会漏掉配置变更（改了 Token 还在用旧客户端），判重过严会让机器人每个刷新周期被反复重建。
/// 因此这里把参与比较的每个字段、以及每个字段的归一化规则（Trim / 大小写 / 去重 / 排序）都逐条锁死。
/// </remarks>
public class TelegramBotConfigTests
{
    /// <summary>
    /// 新建配置的默认值：数组默认空数组而非 null，兜底回复默认关闭
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyArraysAndDisabledFallback()
    {
        var config = new TelegramBotConfig();

        Assert.Equal(0L, config.Id);
        Assert.Equal(string.Empty, config.Name);
        Assert.Equal(string.Empty, config.Token);
        Assert.Empty(config.AdminUsers);
        Assert.Empty(config.AllowedGroupChatIds);
        Assert.Empty(config.AllowedCommands);
        Assert.False(config.EnableFallbackReply);
        Assert.Null(config.Remark);
    }

    /// <summary>
    /// 与 null 比较恒不相同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenOtherNull_ReturnsFalse()
    {
        Assert.False(new TelegramBotConfig().IsSameAs(null));
    }

    /// <summary>
    /// 全部字段一致时判定相同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenAllFieldsEqual_ReturnsTrue()
    {
        var left = CreateConfig();
        var right = CreateConfig();

        Assert.True(left.IsSameAs(right));
        Assert.True(right.IsSameAs(left));
    }

    /// <summary>
    /// 名称按去空白 + 忽略大小写比较
    /// </summary>
    [Theory]
    [InlineData("main-bot")]
    [InlineData("  main-bot  ")]
    [InlineData("MAIN-BOT")]
    public void IsSameAs_NameComparison_IgnoresCaseAndSurroundingWhitespace(string name)
    {
        var left = CreateConfig();
        var right = CreateConfig();
        right.Name = name;

        Assert.True(left.IsSameAs(right));
    }

    /// <summary>
    /// 名称真正不同则判定不同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenNameDiffers_ReturnsFalse()
    {
        var left = CreateConfig();
        var right = CreateConfig();
        right.Name = "other-bot";

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// Token 只去首尾空白，大小写敏感（Token 是凭证，大小写不同就是另一个凭证）
    /// </summary>
    [Fact]
    public void IsSameAs_TokenComparison_TrimsButIsCaseSensitive()
    {
        var trimmed = CreateConfig();
        trimmed.Token = "  123456:AAHfake-telegram-token  ";
        Assert.True(CreateConfig().IsSameAs(trimmed));

        var recased = CreateConfig();
        recased.Token = "123456:aahfake-telegram-token";
        Assert.False(CreateConfig().IsSameAs(recased));
    }

    /// <summary>
    /// 配置 Id 变化视为不同（数据库来源换了一条记录）
    /// </summary>
    [Fact]
    public void IsSameAs_WhenIdDiffers_ReturnsFalse()
    {
        var left = CreateConfig();
        var right = CreateConfig();
        right.Id = 999L;

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// 兜底回复开关变化视为不同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenFallbackFlagDiffers_ReturnsFalse()
    {
        var left = CreateConfig();
        var right = CreateConfig();
        right.EnableFallbackReply = !right.EnableFallbackReply;

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// 管理员列表比较前会剔除非正数、去重并排序，顺序与重复不影响判定
    /// </summary>
    [Fact]
    public void IsSameAs_AdminUsers_IgnoreOrderDuplicatesAndNonPositiveValues()
    {
        var left = CreateConfig();
        left.AdminUsers = [10L, 20L];

        var right = CreateConfig();
        right.AdminUsers = [20L, 20L, 10L, 0L, -5L];

        Assert.True(left.IsSameAs(right));
    }

    /// <summary>
    /// 管理员集合真正不同则判定不同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenAdminUsersDiffer_ReturnsFalse()
    {
        var left = CreateConfig();
        left.AdminUsers = [10L];

        var right = CreateConfig();
        right.AdminUsers = [10L, 11L];

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// 群组白名单比较前剔除 0、去重并排序
    /// </summary>
    [Fact]
    public void IsSameAs_AllowedGroupChatIds_IgnoreOrderDuplicatesAndZero()
    {
        var left = CreateConfig();
        left.AllowedGroupChatIds = [-100123L, -100456L];

        var right = CreateConfig();
        right.AllowedGroupChatIds = [-100456L, 0L, -100123L, -100456L];

        Assert.True(left.IsSameAs(right));
    }

    /// <summary>
    /// 群组白名单真正不同则判定不同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenAllowedGroupChatIdsDiffer_ReturnsFalse()
    {
        var left = CreateConfig();
        left.AllowedGroupChatIds = [-100123L];

        var right = CreateConfig();
        right.AllowedGroupChatIds = [-100999L];

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// 命令白名单比较时忽略顺序、空白项与大小写
    /// </summary>
    [Fact]
    public void IsSameAs_AllowedCommands_IgnoreOrderBlankEntriesAndCase()
    {
        var left = CreateConfig();
        left.AllowedCommands = ["/order", "/help"];

        var right = CreateConfig();
        right.AllowedCommands = [" /HELP ", string.Empty, "   ", "/Order"];

        Assert.True(left.IsSameAs(right));
    }

    /// <summary>
    /// 命令白名单真正不同则判定不同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenAllowedCommandsDiffer_ReturnsFalse()
    {
        var left = CreateConfig();
        left.AllowedCommands = ["/order"];

        var right = CreateConfig();
        right.AllowedCommands = ["/order", "/ban"];

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// 备注不参与比较（纯展示字段，改备注不该触发机器人重启）
    /// </summary>
    [Fact]
    public void IsSameAs_RemarkIsNotCompared()
    {
        var left = CreateConfig();
        left.Remark = "线上主机器人";

        var right = CreateConfig();
        right.Remark = null;

        Assert.True(left.IsSameAs(right));
    }

    /// <summary>
    /// 数组字段为 null 时按空集合处理，不抛空引用
    /// </summary>
    [Fact]
    public void IsSameAs_WhenArrayFieldsNull_TreatsThemAsEmpty()
    {
        var left = CreateConfig();
        left.AdminUsers = null!;
        left.AllowedGroupChatIds = null!;
        left.AllowedCommands = null!;

        var right = CreateConfig();

        Assert.True(left.IsSameAs(right));
        Assert.True(right.IsSameAs(left));
    }

    /// <summary>
    /// 与自身比较恒为真
    /// </summary>
    [Fact]
    public void IsSameAs_WithItself_ReturnsTrue()
    {
        var config = CreateConfig();

        Assert.True(config.IsSameAs(config));
    }

    /// <summary>
    /// 构造一份基准配置
    /// </summary>
    /// <returns>机器人配置</returns>
    private static TelegramBotConfig CreateConfig()
    {
        return new TelegramBotConfig
        {
            Id = 7L,
            Name = "main-bot",
            Token = "123456:AAHfake-telegram-token",
            AdminUsers = [],
            AllowedGroupChatIds = [],
            AllowedCommands = [],
            EnableFallbackReply = true
        };
    }
}
