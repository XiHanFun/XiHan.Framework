// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Template;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotTemplate"/> 测试
/// </summary>
/// <remarks>
/// 模板默认类型是 Markdown 而不是 Text——与 <c>BotMessage</c> 的默认值不同，这是刻意的：
/// 走模板的多半是格式化告警。这个差异容易被"顺手统一"掉，所以单独锁一条。
/// </remarks>
public class BotTemplateTests
{
    /// <summary>
    /// 默认值：名称与内容空串、类型 Markdown、扩展数据为空字典
    /// </summary>
    [Fact]
    public void Defaults_AreMarkdownTemplate()
    {
        var template = new BotTemplate();

        Assert.Equal(string.Empty, template.Name);
        Assert.Null(template.Title);
        Assert.Equal(string.Empty, template.Content);
        Assert.Equal(BotMessageType.Markdown, template.Type);
        Assert.NotNull(template.Data);
        Assert.Empty(template.Data);
    }

    /// <summary>
    /// 模板默认类型与消息默认类型刻意不同
    /// </summary>
    [Fact]
    public void DefaultType_DiffersFromMessageDefault()
    {
        Assert.NotEqual(new BotMessage().Type, new BotTemplate().Type);
    }

    /// <summary>
    /// 扩展数据的键名大小写不敏感
    /// </summary>
    [Fact]
    public void Data_KeyLookupIsCaseInsensitive()
    {
        var template = new BotTemplate();

        template.Data["Strategy"] = "Failover";

        Assert.True(template.Data.ContainsKey("strategy"));
        Assert.Equal("Failover", template.Data["STRATEGY"]);
    }
}
