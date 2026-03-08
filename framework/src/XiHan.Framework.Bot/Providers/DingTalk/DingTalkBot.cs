#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DingTalkBot
// Guid:ca83eb21-6b7e-4036-8bef-5c275a05228b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Dtos;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Bot.Providers.DingTalk;

/// <summary>
/// 钉钉自定义机器人消息推送
/// https://open.dingtalk.com/document/orgapp/custom-robot-access
/// 每个机器人每分钟最多发送20条消息到群里，如果超过20条，会限流10分钟
/// </summary>
public class DingTalkBot
{
    private readonly string _url;
    private readonly string _secret;
    private readonly string? _keyWord;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dingTalkConnection"></param>
    public DingTalkBot(DingTalkConnection dingTalkConnection)
    {
        _url = dingTalkConnection.WebHookUrl + "?access_token=" + dingTalkConnection.AccessToken;
        _secret = dingTalkConnection.Secret;
        _keyWord = dingTalkConnection.KeyWord == null ? null : dingTalkConnection.KeyWord + "\n";
    }

    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="dingTalkText">内容</param>
    /// <param name="dingTalkAt">指定目标人群</param>
    /// <returns></returns>
    public async Task<BotResult> TextMessage(DingTalkText dingTalkText, DingTalkAt? dingTalkAt)
    {
        var msgType = DingTalkMsgTypeEnum.Text.GetDescription();
        dingTalkText.Content = _keyWord + dingTalkText.Content;
        var result = await Send(new { msgtype = msgType, text = dingTalkText, at = dingTalkAt });
        return result;
    }

    /// <summary>
    /// 发送链接消息
    /// </summary>
    /// <param name="dingTalkLink"></param>
    public async Task<BotResult> LinkMessage(DingTalkLink dingTalkLink)
    {
        var msgType = DingTalkMsgTypeEnum.Link.GetDescription();
        dingTalkLink.Title = _keyWord + dingTalkLink.Title;
        var result = await Send(new { msgtype = msgType, link = dingTalkLink });
        return result;
    }

    /// <summary>
    /// 发送文档消息
    /// </summary>
    /// <param name="dingTalkMarkdown">Markdown内容</param>
    /// <param name="dingTalkAt">指定目标人群</param>
    public async Task<BotResult> MarkdownMessage(DingTalkMarkdown dingTalkMarkdown, DingTalkAt? dingTalkAt)
    {
        var msgType = DingTalkMsgTypeEnum.Markdown.GetDescription();
        dingTalkMarkdown.Title = _keyWord + dingTalkMarkdown.Title;
        var result = await Send(new { msgtype = msgType, markdown = dingTalkMarkdown, at = dingTalkAt });
        return result;
    }

    /// <summary>
    /// 发送任务卡片消息
    /// 按钮方案二选一，设置单个按钮方案后多个按钮方案会无效
    /// </summary>
    /// <param name="dingTalkActionCard">ActionCard内容</param>
    public async Task<BotResult> ActionCardMessage(DingTalkActionCard dingTalkActionCard)
    {
        var msgType = DingTalkMsgTypeEnum.ActionCard.GetDescription();
        dingTalkActionCard.Title = _keyWord + dingTalkActionCard.Title;
        dingTalkActionCard.Btns?.ForEach(btn => btn.Title = _keyWord + btn.Title);
        var result = await Send(new { msgtype = msgType, actionCard = dingTalkActionCard });
        return result;
    }

    /// <summary>
    /// 发送卡片菜单消息
    /// </summary>
    /// <param name="dingTalkFeedCard">FeedCard内容</param>
    public async Task<BotResult> FeedCardMessage(DingTalkFeedCard dingTalkFeedCard)
    {
        var msgType = DingTalkMsgTypeEnum.FeedCard.GetDescription();
        dingTalkFeedCard.Links?.ForEach(link => link.Title = _keyWord + link.Title);
        var result = await Send(new { msgtype = msgType, feedCard = dingTalkFeedCard });
        return result;
    }

    /// <summary>
    /// 钉钉执行发送消息
    /// </summary>
    /// <param name="objSend"></param>
    /// <returns></returns>
    private async Task<BotResult> Send(object objSend)
    {
        // 把 【timestamp + "\n" + 密钥】 当做签名字符串
        var timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var message = timeStamp + "\n" + _secret;
        var sign = HmacHelper.HmacSha256(_secret, message).UrlEncode();

        // 发起请求
        var url = _url + $"&timestamp={timeStamp}&sign={sign}";
        var request = await url.AsHttp().SetJsonBody(objSend).PostAsync<DingTalkResultInfoDto>();

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
            var resultInfos = EnumHelper.GetTypedEnumItems<DingTalkResultErrCodeEnum>();
            var info = resultInfos.FirstOrDefault(e => e.Value.ConvertToInt() == result.ErrCode);
            return BotResult.BadRequest("发送失败；" + info?.Description);
        }
    }
}
