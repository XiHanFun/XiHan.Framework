// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Email.Options;

namespace XiHan.Framework.Bot.Email.Tests.Options;

/// <summary>
/// <see cref="EmailOptions"/> 默认值与语义测试
/// </summary>
/// <remarks>
/// EmailOptions 没有 Validate() 方法，非法配置由 EmailBotProvider 在发送前拦截，
/// 因此这里只锁死"默认值语义"，校验行为在 EmailBotProviderTests 中覆盖。
/// </remarks>
public class EmailOptionsTests
{
    /// <summary>
    /// 默认启用，默认走 HTML 正文
    /// </summary>
    /// <remarks>
    /// 这两个默认值决定"只填发件人就能用"，属对外承诺，改动会静默影响所有既有配置。
    /// </remarks>
    [Fact]
    public void Defaults_EnabledAndHtmlBody_AreTrue()
    {
        var options = new EmailOptions();

        Assert.True(options.Enabled);
        Assert.True(options.IsBodyHtml);
    }

    /// <summary>
    /// 默认发件人配置非空且为全新实例
    /// </summary>
    [Fact]
    public void Defaults_From_IsNotNullAndPerInstance()
    {
        var first = new EmailOptions();
        var second = new EmailOptions();

        Assert.NotNull(first.From);
        Assert.NotNull(second.From);
        // 默认值必须是每实例独立的，否则两份配置会互相污染发件人
        Assert.NotSame(first.From, second.From);
    }

    /// <summary>
    /// 默认收件人/抄送/密送均为空集合而非 null
    /// </summary>
    /// <remarks>
    /// EmailBotProvider 直接把这三个集合当作兜底列表使用且不做 null 判断，
    /// 一旦默认值变成 null 会直接抛 NullReferenceException。
    /// </remarks>
    [Fact]
    public void Defaults_RecipientLists_AreEmptyNotNull()
    {
        var options = new EmailOptions();

        Assert.NotNull(options.To);
        Assert.NotNull(options.Cc);
        Assert.NotNull(options.Bcc);
        Assert.Empty(options.To);
        Assert.Empty(options.Cc);
        Assert.Empty(options.Bcc);
    }

    /// <summary>
    /// 默认收件人集合每实例独立
    /// </summary>
    [Fact]
    public void Defaults_RecipientLists_ArePerInstance()
    {
        var first = new EmailOptions();
        var second = new EmailOptions();

        first.To.Add("a@example.com");

        Assert.Empty(second.To);
    }

    /// <summary>
    /// 所有属性均可写，支持 Configure 委托方式赋值
    /// </summary>
    [Fact]
    public void Properties_AreMutable_ForConfigureDelegate()
    {
        var options = new EmailOptions
        {
            Enabled = false,
            IsBodyHtml = false,
            To = ["to@example.com"],
            Cc = ["cc@example.com"],
            Bcc = ["bcc@example.com"]
        };
        options.From.SmtpHost = "smtp.example.com";

        Assert.False(options.Enabled);
        Assert.False(options.IsBodyHtml);
        Assert.Single(options.To);
        Assert.Equal("to@example.com", options.To[0]);
        Assert.Single(options.Cc);
        Assert.Equal("cc@example.com", options.Cc[0]);
        Assert.Single(options.Bcc);
        Assert.Equal("bcc@example.com", options.Bcc[0]);
        Assert.Equal("smtp.example.com", options.From.SmtpHost);
    }
}
