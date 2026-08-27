// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotChannel"/> 测试
/// </summary>
/// <remarks>
/// 渠道定义直接来自配置绑定，默认值必须保证"没配 Providers"时是空集合而不是 null。
/// </remarks>
public class BotChannelTests
{
    /// <summary>
    /// 默认值：名称空串、提供者列表为空集合、描述为 null
    /// </summary>
    [Fact]
    public void Defaults_AreEmpty()
    {
        var channel = new BotChannel();

        Assert.Equal(string.Empty, channel.Name);
        Assert.NotNull(channel.Providers);
        Assert.Empty(channel.Providers);
        Assert.Null(channel.Description);
    }

    /// <summary>
    /// JSON 往返保留名称、提供者列表与描述
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsFields()
    {
        var channel = new BotChannel
        {
            Name = "ops",
            Providers = new List<string> { "DingTalk", "Lark" },
            Description = "运维群"
        };

        var json = JsonSerializer.Serialize(channel);
        var restored = JsonSerializer.Deserialize<BotChannel>(json);

        Assert.NotNull(restored);
        Assert.Equal("ops", restored!.Name);
        Assert.Equal("运维群", restored.Description);
        Assert.Equal(2, restored.Providers.Count);
        Assert.Contains("DingTalk", restored.Providers);
        Assert.Contains("Lark", restored.Providers);
    }
}
