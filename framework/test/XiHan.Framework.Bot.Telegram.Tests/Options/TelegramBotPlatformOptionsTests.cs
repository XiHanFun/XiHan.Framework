// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramBotPlatformOptions"/> 平台选项测试
/// </summary>
/// <remarks>
/// SectionName 是配置文件契约，改一个字符所有存量 appsettings 的 Telegram 段就整体失效，
/// 因此必须按字面量锁死。
/// </remarks>
public class TelegramBotPlatformOptionsTests
{
    /// <summary>
    /// 配置节名称锁死为 XiHan:Bot:Telegram:Platform
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationContract()
    {
        Assert.Equal("XiHan:Bot:Telegram:Platform", TelegramBotPlatformOptions.SectionName);
    }

    /// <summary>
    /// 四个子对象默认均已实例化，绑定缺省配置时不会得到 null
    /// </summary>
    [Fact]
    public void Defaults_AllSectionsAreInitialized()
    {
        var options = new TelegramBotPlatformOptions();

        Assert.NotNull(options.Settings);
        Assert.NotNull(options.Bots);
        Assert.NotNull(options.Retry);
        Assert.NotNull(options.Texts);
    }

    /// <summary>
    /// 默认没有任何机器人配置
    /// </summary>
    [Fact]
    public void Defaults_BotsIsEmpty()
    {
        Assert.Empty(new TelegramBotPlatformOptions().Bots);
    }

    /// <summary>
    /// 默认平台未启用，与「引入依赖不等于自动上线」的基线一致
    /// </summary>
    [Fact]
    public void Defaults_PlatformIsDisabled()
    {
        Assert.False(new TelegramBotPlatformOptions().Settings.Enabled);
    }

    /// <summary>
    /// 每个实例持有独立的子对象，互不串改
    /// </summary>
    [Fact]
    public void Defaults_SectionsAreNotSharedBetweenInstances()
    {
        var first = new TelegramBotPlatformOptions();
        var second = new TelegramBotPlatformOptions();

        first.Settings.Enabled = true;
        first.Retry.MaxRetries = 99;
        first.Bots.Add(new TelegramBotConfig());

        Assert.False(second.Settings.Enabled);
        Assert.Equal(3, second.Retry.MaxRetries);
        Assert.Empty(second.Bots);
    }
}
