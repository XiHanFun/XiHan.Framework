// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net.Mail;
using XiHan.Framework.Bot.Email.Models;

namespace XiHan.Framework.Bot.Email.Tests.Models;

/// <summary>
/// <see cref="EmailToModel"/> 默认值与集合契约测试
/// </summary>
/// <remarks>
/// EmailBot.SendMail 对 ToMail/CcMail/BccMail/AttachmentsPath 直接调用 ForEach 与 Count，
/// 因此"默认空集合而非 null"是硬契约；附件类型固定为 BCL 的 Attachment，不是 MimeKit 类型。
/// </remarks>
public class EmailToModelTests
{
    /// <summary>
    /// 主题与正文默认为空串，默认走 HTML 正文
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyTextWithHtmlBody()
    {
        var model = new EmailToModel();

        Assert.Equal(string.Empty, model.Subject);
        Assert.Equal(string.Empty, model.Body);
        Assert.True(model.IsBodyHtml);
    }

    /// <summary>
    /// 四个集合默认为空集合而非 null
    /// </summary>
    [Fact]
    public void Collections_AreEmptyNotNull()
    {
        var model = new EmailToModel();

        Assert.NotNull(model.ToMail);
        Assert.NotNull(model.CcMail);
        Assert.NotNull(model.BccMail);
        Assert.NotNull(model.AttachmentsPath);
        Assert.Empty(model.ToMail);
        Assert.Empty(model.CcMail);
        Assert.Empty(model.BccMail);
        Assert.Empty(model.AttachmentsPath);
    }

    /// <summary>
    /// 默认集合每实例独立，互不污染
    /// </summary>
    [Fact]
    public void Collections_ArePerInstance()
    {
        var first = new EmailToModel();
        var second = new EmailToModel();

        first.ToMail.Add("a@example.com");
        first.CcMail.Add("b@example.com");
        first.BccMail.Add("c@example.com");

        Assert.Empty(second.ToMail);
        Assert.Empty(second.CcMail);
        Assert.Empty(second.BccMail);
    }

    /// <summary>
    /// 附件集合接受 System.Net.Mail.Attachment 并保留名称与内容流
    /// </summary>
    [Fact]
    public void AttachmentsPath_AcceptsNetMailAttachment()
    {
        using var stream = new MemoryStream("hello"u8.ToArray());
        using var attachment = new Attachment(stream, "hello.txt");
        var model = new EmailToModel
        {
            AttachmentsPath = [attachment]
        };

        Assert.Single(model.AttachmentsPath);
        Assert.Equal("hello.txt", model.AttachmentsPath[0].Name);
        Assert.NotNull(model.AttachmentsPath[0].ContentStream);
    }

    /// <summary>
    /// 可整体替换收件人集合
    /// </summary>
    [Fact]
    public void Recipients_CanBeReplacedWholesale()
    {
        var model = new EmailToModel
        {
            Subject = "标题",
            Body = "正文",
            IsBodyHtml = false,
            ToMail = ["to@example.com"],
            CcMail = ["cc@example.com"],
            BccMail = ["bcc@example.com"]
        };

        Assert.Equal("标题", model.Subject);
        Assert.Equal("正文", model.Body);
        Assert.False(model.IsBodyHtml);
        Assert.Single(model.ToMail);
        Assert.Equal("to@example.com", model.ToMail[0]);
        Assert.Single(model.CcMail);
        Assert.Equal("cc@example.com", model.CcMail[0]);
        Assert.Single(model.BccMail);
        Assert.Equal("bcc@example.com", model.BccMail[0]);
    }
}
