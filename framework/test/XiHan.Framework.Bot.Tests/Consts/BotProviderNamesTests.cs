// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotProviderNames"/> 常量测试
/// </summary>
/// <remarks>
/// 这些字面量既是 DI 里提供者的注册名，也会出现在配置文件的渠道映射里，
/// 改动即破坏既有配置，因此必须锁死取值。
/// </remarks>
public class BotProviderNamesTests
{
    /// <summary>
    /// 各提供者名称取值不漂移
    /// </summary>
    [Fact]
    public void ProviderNames_AreStable()
    {
        Assert.Equal("DingTalk", BotProviderNames.DingTalk);
        Assert.Equal("Lark", BotProviderNames.Lark);
        Assert.Equal("WeCom", BotProviderNames.WeCom);
        Assert.Equal("Telegram", BotProviderNames.Telegram);
        Assert.Equal("Email", BotProviderNames.Email);
        Assert.Equal("Sms", BotProviderNames.Sms);
    }

    /// <summary>
    /// 提供者名称互不重复
    /// </summary>
    [Fact]
    public void ProviderNames_AreDistinct()
    {
        var names = new[]
        {
            BotProviderNames.DingTalk,
            BotProviderNames.Lark,
            BotProviderNames.WeCom,
            BotProviderNames.Telegram,
            BotProviderNames.Email,
            BotProviderNames.Sms
        };

        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
