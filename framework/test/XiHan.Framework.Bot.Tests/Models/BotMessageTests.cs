// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Tests.Models;

/// <summary>
/// <see cref="BotMessage"/> 测试
/// </summary>
/// <remarks>
/// Data 字典的比较器是大小写不敏感，这一点被 <c>BotMessageHelper</c> 与各提供者子包依赖，属于对外契约。
/// </remarks>
public class BotMessageTests
{
    /// <summary>
    /// 默认值：内容空串、类型文本、集合非 null
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyTextMessage()
    {
        var message = new BotMessage();

        Assert.Null(message.Title);
        Assert.Equal(string.Empty, message.Content);
        Assert.Equal(BotMessageType.Text, message.Type);
        Assert.NotNull(message.Mentions);
        Assert.Empty(message.Mentions);
        Assert.NotNull(message.Data);
        Assert.Empty(message.Data);
    }

    /// <summary>
    /// Data 键名大小写不敏感
    /// </summary>
    [Fact]
    public void Data_KeyLookupIsCaseInsensitive()
    {
        var message = new BotMessage();

        message.Data["Strategy"] = "Failover";

        Assert.True(message.Data.ContainsKey("strategy"));
        Assert.True(message.Data.ContainsKey("STRATEGY"));
        Assert.Equal("Failover", message.Data["sTrAtEgY"]);
    }

    /// <summary>
    /// 相同键的不同大小写写入会覆盖而不是新增
    /// </summary>
    [Fact]
    public void Data_SameKeyDifferentCase_Overwrites()
    {
        var message = new BotMessage();

        message.Data["Strategy"] = "Broadcast";
        message.Data["strategy"] = "Priority";

        Assert.Single(message.Data);
        Assert.Equal("Priority", message.Data["Strategy"]);
    }

    /// <summary>
    /// JSON 往返保留标题、内容、类型与提及列表
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsPrimaryFields()
    {
        var message = new BotMessage
        {
            Title = "告警",
            Content = "磁盘将满",
            Type = BotMessageType.Markdown
        };
        message.Mentions.Add("ops");

        var json = JsonSerializer.Serialize(message);

        Assert.Contains("\"Type\":1", json);

        var restored = JsonSerializer.Deserialize<BotMessage>(json);

        Assert.NotNull(restored);
        Assert.Equal("告警", restored!.Title);
        Assert.Equal("磁盘将满", restored.Content);
        Assert.Equal(BotMessageType.Markdown, restored.Type);
        Assert.Single(restored.Mentions);
        Assert.Equal("ops", restored.Mentions[0]);
    }
}
