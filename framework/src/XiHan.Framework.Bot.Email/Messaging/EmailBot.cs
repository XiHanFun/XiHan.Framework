#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmailBot
// Guid:83f59035-2972-4c3d-8feb-372d66e871fd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using MailKit.Net.Smtp;
using MimeKit;
using XiHan.Framework.Utils.Logging;
using XiHan.Framework.Bot.Email.Models;

namespace XiHan.Framework.Bot.Email.Messaging;

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
    /// <param name="toModel">收件模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<bool> SendMail(EmailToModel toModel, CancellationToken cancellationToken = default)
    {
        // 发件人显示名：优先使用 FromName，缺省回退为发件邮箱
        var fromDisplayName = string.IsNullOrWhiteSpace(fromModel.FromName) ? fromModel.FromMail : fromModel.FromName;
        MimeMessage message = new()
        {
            // 来源
            Sender = new MailboxAddress(fromDisplayName, fromModel.FromMail)
        };
        // 发件人地址集合
        message.From.Add(new MailboxAddress(fromDisplayName, fromModel.FromMail));
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
            // 默认走 MailKit 系统证书校验；仅当显式允许（开发环境自签证书）时才放开，避免生产被中间人攻击
            if (fromModel.AcceptInvalidCertificate)
            {
                client.ServerCertificateValidationCallback = (_, _, _, _) => true;
            }
            await client.ConnectAsync(fromModel.SmtpHost, fromModel.SmtpPort, fromModel.UseSsl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(fromModel.FromUserName))
            {
                await client.AuthenticateAsync(fromModel.FromUserName, fromModel.FromPassword, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 调用方取消：原样上抛，不吞成"发送失败"
            throw;
        }
        catch (Exception ex)
        {
            LogHelper.Error($"邮件发送失败:{ex.Message}");
        }

        return false;
    }
}
