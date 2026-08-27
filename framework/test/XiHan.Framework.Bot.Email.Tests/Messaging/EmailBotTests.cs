// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net.Mail;
using XiHan.Framework.Bot.Email.Messaging;
using XiHan.Framework.Bot.Email.Models;

namespace XiHan.Framework.Bot.Email.Tests.Messaging;

/// <summary>
/// <see cref="EmailBot"/> 投递封装测试
/// </summary>
/// <remarks>
/// 绝不连真实 SMTP。所有用例统一把 SmtpHost 设为 localhost、SmtpPort 设为 -1：
/// MailKit 在建立连接之前就会因端口越界抛出参数异常，既不解析外部域名也不打开套接字，
/// 因此这里验证的是 EmailBot 自己的契约——组装 MimeMessage、异常不外泄、失败返回 false。
/// </remarks>
public class EmailBotTests
{
    /// <summary>
    /// 投递失败时返回 false 而不是抛异常
    /// </summary>
    /// <remarks>
    /// 这是 EmailBotProvider 依赖的核心契约：SendMail 把 SMTP 侧的一切异常吞成 false，
    /// 只有调用方主动取消才会原样上抛。
    /// </remarks>
    [Fact]
    public async Task SendMail_WhenSmtpUnusable_ReturnsFalseWithoutThrowing()
    {
        var bot = new EmailBot(CreateUnusableFromModel());
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "<p>正文</p>",
            ToMail = ["receiver@example.com"]
        };

        var result = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// 纯文本正文分支同样安全返回 false
    /// </summary>
    [Fact]
    public async Task SendMail_WithPlainTextBody_ReturnsFalseWithoutThrowing()
    {
        var bot = new EmailBot(CreateUnusableFromModel());
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "纯文本正文",
            IsBodyHtml = false,
            ToMail = ["receiver@example.com"]
        };

        var result = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// 抄送与密送同时存在时不影响消息组装
    /// </summary>
    [Fact]
    public async Task SendMail_WithCcAndBcc_ReturnsFalseWithoutThrowing()
    {
        var bot = new EmailBot(CreateUnusableFromModel());
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "正文",
            ToMail = ["receiver@example.com"],
            CcMail = ["cc@example.com"],
            BccMail = ["bcc@example.com"]
        };

        var result = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// 收件人集合为空时不抛异常
    /// </summary>
    /// <remarks>
    /// EmailBot 自身不做收件人校验，"至少一个收件人"是 EmailBotProvider 的职责；
    /// 这里锁死"空收件人不会把 EmailBot 打崩"，避免上层校验被绕过时炸在底层。
    /// </remarks>
    [Fact]
    public async Task SendMail_WithoutAnyRecipient_ReturnsFalseWithoutThrowing()
    {
        var bot = new EmailBot(CreateUnusableFromModel());
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "正文"
        };

        var result = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// 发件人显示名为空时回退为发件邮箱，不影响组装
    /// </summary>
    [Fact]
    public async Task SendMail_WhenFromNameIsBlank_ReturnsFalseWithoutThrowing()
    {
        var fromModel = CreateUnusableFromModel();
        fromModel.FromName = "   ";
        var bot = new EmailBot(fromModel);
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "正文",
            ToMail = ["receiver@example.com"]
        };

        var result = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// 配置了认证用户名时走认证分支，失败仍返回 false
    /// </summary>
    [Fact]
    public async Task SendMail_WithAuthenticationConfigured_ReturnsFalseWithoutThrowing()
    {
        var fromModel = CreateUnusableFromModel();
        fromModel.FromUserName = "sender@example.com";
        fromModel.FromPassword = "secret";
        var bot = new EmailBot(fromModel);
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "正文",
            ToMail = ["receiver@example.com"]
        };

        var result = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// 显式允许无效证书时同样不抛异常
    /// </summary>
    [Fact]
    public async Task SendMail_WhenAcceptInvalidCertificate_ReturnsFalseWithoutThrowing()
    {
        var fromModel = CreateUnusableFromModel();
        fromModel.AcceptInvalidCertificate = true;
        var bot = new EmailBot(fromModel);
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "正文",
            ToMail = ["receiver@example.com"]
        };

        var result = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// 带附件时能完成组装并安全返回 false
    /// </summary>
    /// <remarks>
    /// 附件走的是 MemoryStream，不落磁盘、不读外部文件。
    /// </remarks>
    [Fact]
    public async Task SendMail_WithAttachment_ReturnsFalseWithoutThrowing()
    {
        using var stream = new MemoryStream("attachment-content"u8.ToArray());
        using var attachment = new Attachment(stream, "note.txt");
        var bot = new EmailBot(CreateUnusableFromModel());
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "正文",
            ToMail = ["receiver@example.com"],
            AttachmentsPath = [attachment]
        };

        var result = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>
    /// 同一实例可重复调用，失败不留下脏状态
    /// </summary>
    /// <remarks>
    /// SmtpClient 是方法内 using 的局部变量，重复调用之间不应互相影响。
    /// </remarks>
    [Fact]
    public async Task SendMail_CalledTwice_IsRepeatable()
    {
        var bot = new EmailBot(CreateUnusableFromModel());
        var toModel = new EmailToModel
        {
            Subject = "标题",
            Body = "正文",
            ToMail = ["receiver@example.com"]
        };

        var first = await bot.SendMail(toModel, TestContext.Current.CancellationToken);
        var second = await bot.SendMail(toModel, TestContext.Current.CancellationToken);

        Assert.False(first);
        Assert.False(second);
    }

    /// <summary>
    /// 调用方主动取消时原样上抛的分支需要真实连接过程
    /// </summary>
    [Fact]
    public void SendMail_WhenCallerCancels_RequiresRealSmtp()
    {
        Assert.Skip("取消分支只在真实 SMTP 连接/认证/投递过程中才会命中，需要真实凭据与网络，CI 不具备");
    }

    /// <summary>
    /// 投递成功返回 true 的路径需要真实 SMTP 服务
    /// </summary>
    [Fact]
    public void SendMail_SuccessPath_RequiresRealSmtp()
    {
        Assert.Skip("需要真实 SMTP 凭据与网络，CI 不具备");
    }

    /// <summary>
    /// 构造一份地址合法、但 SMTP 端口越界的发件人配置
    /// </summary>
    private static EmailFromModel CreateUnusableFromModel()
    {
        return new EmailFromModel
        {
            SmtpHost = "localhost",
            // 端口越界：MailKit 在连接前的参数校验阶段即失败，全程无网络 IO
            SmtpPort = -1,
            UseSsl = false,
            FromMail = "sender@example.com",
            FromName = "曦寒"
        };
    }
}
