#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LarkBot
// Guid:82700645-d746-4825-9c95-d3980abc4052
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers.DingTalk;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Bot.Providers.Lark;

/// <summary>
/// 飞书自定义机器人消息推送
/// https://open.feishu.cn/document/client-docs/bot-v3/add-custom-bot
/// 自定义机器人的频率控制和普通应用不同，为 100 次/分钟，5 次/秒
/// </summary>
public class LarkBot
{
    private readonly string _url;
    private readonly string _secret;
    private readonly string? _keyWord;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="larkConnection"></param>
    public LarkBot(LarkOptions larkConnection)
    {
        _url = larkConnection.WebHookUrl + "/" + larkConnection.AccessToken;
        _secret = larkConnection.Secret;
        _keyWord = larkConnection.KeyWord == null ? null : larkConnection.KeyWord + "\n";
    }

    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="larkText">内容</param>
    /// <returns></returns>
    public async Task<BotResult> TextMessage(LarkText larkText)
    {
        var msgType = LarkMsgTypeEnum.Text.GetDescription();
        larkText.Text = _keyWord + larkText.Text;
        var result = await Send(new { msg_type = msgType, content = larkText });
        return result;
    }

    /// <summary>
    /// 发送富文本消息
    /// </summary>
    /// <param name="larkPost">Post内容</param>
    public async Task<BotResult> PostMessage(LarkPost larkPost)
    {
        var msgType = LarkMsgTypeEnum.Post.GetDescription();
        var objTList = new List<List<object>>();

        // 拆解内容
        var contentTList = larkPost.Content;
        foreach (var contentList in contentTList)
        {
            var list = new List<object>();
            foreach (var itemT in contentList)
            {
                switch (itemT)
                {
                    case TagText text:
                        list.Add(text);
                        break;

                    case TagA a:
                        list.Add(a);
                        break;

                    case TagAt at:
                        list.Add(at);
                        break;

                    case TagImg image:
                        list.Add(image);
                        break;

                    default:
                        Console.WriteLine("Unknown type");
                        break;
                }
            }
            objTList.Add(list);
        }
        larkPost.Title = _keyWord + larkPost.Title;
        var zhCn = new { title = larkPost.Title, content = objTList };
        // 设置语言
        var post = new { zh_cn = zhCn };
        var postContent = new { post };
        var result = await Send(new { msg_type = msgType, content = postContent });
        return result;
    }

    /// <summary>
    /// 发送图片消息
    /// </summary>
    /// <param name="larkImage">Image内容</param>
    public async Task<BotResult> ImageMessage(LarkImage larkImage)
    {
        var msgType = LarkMsgTypeEnum.Image.GetDescription();
        var result = await Send(new { msg_type = msgType, content = larkImage });
        return result;
    }

    /// <summary>
    /// 发送消息卡片
    /// </summary>
    /// <param name="larkInterActive">InterActive内容</param>
    public async Task<BotResult> InterActiveMessage(LarkInterActive larkInterActive)
    {
        var msgType = LarkMsgTypeEnum.InterActive.GetDescription();
        larkInterActive.Header.Title.Content = _keyWord + larkInterActive.Header.Title.Content;
        var result = await Send(new { msg_type = msgType, card = larkInterActive });
        return result;
    }

    /// <summary>
    /// 飞书执行发送消息
    /// </summary>
    /// <param name="objSend"></param>
    /// <returns></returns>
    private async Task<BotResult> Send(object objSend)
    {
        // 把 【timestamp + "\n" + 密钥】 当做签名字符串
        var timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var message = timeStamp + "\n" + _secret;
        var sign = HmacHelper.HmacSha256(_secret, message).UrlEncode();

        // 得到最终的请求体
        objSend.SetPropertyValue("timeStamp", timeStamp);
        objSend.SetPropertyValue("sign", sign);

        // 发起请求
        var url = _url;
        var request = await url.AsHttp().SetJsonBody(objSend).PostAsync<LarkResultInfoDto>();

        if (!request.IsSuccess || request.Data == null)
        {
            return BotResult.BadRequest(request.RawDataString);
        }

        var result = request.Data;

        if (result.Code == 0 || result.Msg == "success")
        {
            return BotResult.Success("发送成功；");
        }
        else
        {
            var resultInfos = EnumHelper.GetTypedEnumItems<DingTalkResultErrCodeEnum>();
            var info = resultInfos.FirstOrDefault(e => e.Value.ConvertToInt() == result.Code);
            return BotResult.BadRequest("发送失败；" + info?.Description);
        }
    }
}
