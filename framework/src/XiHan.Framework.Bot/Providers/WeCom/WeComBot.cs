#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WeComBot
// Guid:1f9edb73-56c9-4849-88a8-c57488b3582d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Dtos;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Bot.Providers.WeCom;

/// <summary>
/// 企业微信自定义机器人消息推送
/// https://developer.work.weixin.qq.com/document/path/91770
/// 每个机器人发送的消息不能超过20条/分钟
/// </summary>
public class WeComBot
{
    private readonly string _messageUrl;

    // 文件上传地址，调用接口凭证, 机器人 webhook 中的 key 参数
    private readonly string _uploadUrl;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="weChatConnection"></param>
    public WeComBot(WeComConnection weChatConnection)
    {
        _messageUrl = weChatConnection.WebHookUrl + "?key=" + weChatConnection.Key;
        _uploadUrl = weChatConnection.UploadUrl + "?key=" + weChatConnection.Key;
    }

    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="weComText">内容</param>
    /// <returns></returns>
    public async Task<BotResult> TextMessage(WeComText weComText)
    {
        var msgType = WeComMsgTypeEnum.Text.GetDescription();
        var result = await SendMessage(new
        {
            msgtype = msgType,
            text = weComText
        });
        return result;
    }

    /// <summary>
    /// 发送文档消息
    /// </summary>
    /// <param name="weComMarkdown">文档</param>
    /// <returns></returns>
    public async Task<BotResult> MarkdownMessage(WeComMarkdown weComMarkdown)
    {
        var msgType = WeComMsgTypeEnum.Markdown.GetDescription();
        var result = await SendMessage(new
        {
            msgtype = msgType,
            markdown = weComMarkdown
        });
        return result;
    }

    /// <summary>
    /// 发送图片消息
    /// </summary>
    /// <param name="weComImage">图片</param>
    /// <returns></returns>
    public async Task<BotResult> ImageMessage(WeComImage weComImage)
    {
        var msgType = WeComMsgTypeEnum.Image.GetDescription();
        var result = await SendMessage(new
        {
            msgtype = msgType,
            image = weComImage
        });
        return result;
    }

    /// <summary>
    /// 发送图文消息
    /// </summary>
    /// <param name="weComNews">图文</param>
    /// <returns></returns>
    public async Task<BotResult> NewsMessage(WeComNews weComNews)
    {
        var msgType = WeComMsgTypeEnum.News.GetDescription();
        var result = await SendMessage(new
        {
            msgtype = msgType,
            news = weComNews
        });
        return result;
    }

    /// <summary>
    /// 发送文件消息
    /// </summary>
    /// <param name="weComFile">文件</param>
    /// <returns></returns>
    public async Task<BotResult> FileMessage(WeComFile weComFile)
    {
        var msgType = WeComMsgTypeEnum.File.GetDescription();
        var result = await SendMessage(new
        {
            msgtype = msgType,
            file = weComFile
        });
        return result;
    }

    /// <summary>
    /// 发送语音消息
    /// </summary>
    /// <param name="weComVoice">语音</param>
    /// <returns></returns>
    public async Task<BotResult> VoiceMessage(WeComVoice weComVoice)
    {
        var msgType = WeComMsgTypeEnum.Voice.GetDescription();
        var result = await SendMessage(new
        {
            msgtype = msgType,
            voice = weComVoice
        });
        return result;
    }

    /// <summary>
    /// 发送文本通知模版卡片消息
    /// </summary>
    /// <param name="weComTemplateCardTextNotice">模版卡片</param>
    /// <returns></returns>
    public async Task<BotResult> TextNoticeMessage(WeComTemplateCardTextNotice weComTemplateCardTextNotice)
    {
        var msgType = WeComMsgTypeEnum.TemplateCard.GetDescription();
        weComTemplateCardTextNotice.CardType = WeComTemplateCardType.TextNotice.GetDescription();
        var result = await SendMessage(new
        {
            msgtype = msgType,
            template_card = weComTemplateCardTextNotice
        });
        return result;
    }

    /// <summary>
    /// 发送图文展示模版卡片消息
    /// </summary>
    /// <param name="weComTemplateCardNewsNotice">模版卡片</param>
    /// <returns></returns>
    public async Task<BotResult> NewsNoticeMessage(WeComTemplateCardNewsNotice weComTemplateCardNewsNotice)
    {
        var msgType = WeComMsgTypeEnum.TemplateCard.GetDescription();
        weComTemplateCardNewsNotice.CardType = WeComTemplateCardType.NewsNotice.GetDescription();
        var result = await SendMessage(new
        {
            msgtype = msgType,
            template_card = weComTemplateCardNewsNotice
        });
        return result;
    }

    /// <summary>
    /// 微信执行上传文件
    /// </summary>
    /// <param name="fileStream">文件流</param>
    /// <param name="uploadType">文件上传类型</param>
    /// <returns></returns>
    /// <remarks>
    /// 素材上传得到media_id，该media_id仅三天内有效，且只能对应上传文件的机器人可以使用
    /// 普通文件(file)：文件大小不超过20M
    /// 语音文件(voice)：文件大小不超过2M，播放长度不超过60s，仅支持AMR格式
    /// </remarks>
    public async Task<BotResult> UploadFile(FileStream fileStream, WeComUploadType uploadType)
    {
        var headers = new Dictionary<string, string>()
        {
            { "filename", fileStream.Name },
            { "filelength", fileStream.Length.ToString() }
        };

        var type = uploadType switch
        {
            WeComUploadType.File => "&type=file",
            WeComUploadType.Voice => "&type=voice",
            _ => string.Empty
        };

        // 发起请求
        var url = _uploadUrl + type;
        var request = await url.AsHttp().SetBody(fileStream).SetHeaders(headers).PostAsync<WeComResultInfoDto>();

        if (!request.IsSuccess || request.Data == null)
        {
            return BotResult.BadRequest(request.RawDataString);
        }

        var result = request.Data;

        if (result.ErrCode == 0 || result.ErrMsg == "ok")
        {
            WeComUploadResultDto uploadResult = new()
            {
                Message = "上传成功；",
                Type = result.Type,
                MediaId = result.MediaId
            };
            return BotResult.Success(uploadResult);
        }
        else
        {
            return BotResult.BadRequest("上传失败；");
        }
    }

    /// <summary>
    /// 微信执行发送消息
    /// </summary>
    /// <param name="objSend"></param>
    /// <returns></returns>
    private async Task<BotResult> SendMessage(object objSend)
    {
        // 发起请求
        var url = _messageUrl;
        var request = await url.AsHttp().SetJsonBody(objSend).PostAsync<WeComResultInfoDto>();

        if (!request.IsSuccess || request.Data == null)
        {
            return BotResult.BadRequest(request.RawDataString);
        }

        var result = request.Data;

        if (result.ErrCode == 0 || result.ErrMsg == "ok")
        {
            return BotResult.Success("发送成功；");
        }
        else
        {
            return BotResult.BadRequest("发送失败；");
        }
    }
}
