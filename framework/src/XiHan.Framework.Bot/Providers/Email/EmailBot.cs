#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmailBot
// Guid:6cd058a5-9ec5-4ab3-8c7d-aeab0f513700
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using MailKit.Net.Smtp;
using MimeKit;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Bot.Providers.Email;

/// <summary>
/// 邮件机器人消息推送
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
/// <param name="fromModel"></param>
public class EmailBot(EmailFromModel fromModel)
{
    /// <summary>
    /// 发送邮件
    /// </summary>
    public async Task<bool> SendMail(EmailToModel toModel)
    {
        MimeMessage message = new()
        {
            // 来源
            Sender = new MailboxAddress(fromModel.FromMail, fromModel.FromMail)
        };
        // 发件人地址集合
        message.From.Add(new MailboxAddress(fromModel.FromMail, fromModel.FromMail));
        // 收件人地址集合
        toModel.ToMail.ForEach(to => message.To.Add(new MailboxAddress(to, to)));
        // 抄送人地址集合
        toModel.CcMail.ForEach(cc => message.Cc.Add(new MailboxAddress(cc, cc)));
        // 密送人地址集合
        toModel.BccMail.ForEach(bcc => message.Bcc.Add(new MailboxAddress(bcc, bcc)));
        // 邮件主题
        message.Subject = toModel.Subject;
        // 邮件日期
        message.Date = DateTime.Now;
        // 邮件正文
        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = toModel.IsBodyHtml ? toModel.Body : null,
            TextBody = toModel.IsBodyHtml ? null : toModel.Body
        };
        //在有附件的情况下添加附件
        try
        {
            if (toModel.AttachmentsPath.Count > 0)
            {
                toModel.AttachmentsPath.ForEach(attachmentFile =>
                {
                    MimePart attachment = new()
                    {
                        Content = new MimeContent(attachmentFile.ContentStream),
                        // 读取文件只能用绝对路径
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = attachmentFile.Name
                    };
                    bodyBuilder.Attachments.Add(attachment);
                });
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"在添加附件时有错误:{ex}");
        }

        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using SmtpClient client = new();
            // 解决远程证书验证无效
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
            await client.ConnectAsync(fromModel.SmtpHost, fromModel.SmtpPort, fromModel.UseSsl);
            await client.AuthenticateAsync(fromModel.FromUserName, fromModel.FromPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error($"邮件发送失败:{ex.Message}");
        }

        return false;
    }
}
