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

using System.Text;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Bot.Lark;

/// <summary>
/// 飞书自定义机器人消息推送
/// https://open.feishu.cn/document/client-docs/bot-v3/add-custom-bot
/// 自定义机器人的频率控制和普通应用不同，为 100 次/分钟，5 次/秒
/// </summary>
public class LarkBot
{
    /// <summary>
    /// 普通消息载荷字段名
    /// </summary>
    private const string ContentBodyKey = "content";

    /// <summary>
    /// 卡片消息载荷字段名
    /// </summary>
    private const string CardBodyKey = "card";

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
        var result = await Send(msgType, ContentBodyKey, larkText);
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
        var result = await Send(msgType, ContentBodyKey, postContent);
        return result;
    }

    /// <summary>
    /// 发送图片消息
    /// </summary>
    /// <param name="larkImage">Image内容</param>
    public async Task<BotResult> ImageMessage(LarkImage larkImage)
    {
        var msgType = LarkMsgTypeEnum.Image.GetDescription();
        var result = await Send(msgType, ContentBodyKey, larkImage);
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
        var result = await Send(msgType, CardBodyKey, larkInterActive);
        return result;
    }

    /// <summary>
    /// 飞书执行发送消息
    /// </summary>
    /// <param name="msgType">消息类型</param>
    /// <param name="bodyKey">载荷字段名（普通消息为 content，卡片消息为 card）</param>
    /// <param name="body">载荷内容</param>
    /// <returns></returns>
    private async Task<BotResult> Send(string msgType, string bodyKey, object body)
    {
        // 构造真实载荷对象：签名字段（timestamp/sign）与消息字段平级
        var payload = new Dictionary<string, object>
        {
            ["msg_type"] = msgType,
            [bodyKey] = body
        };

        // 配置了密钥才附加签名（飞书自定义机器人的签名校验为可选安全设置）
        if (!string.IsNullOrWhiteSpace(_secret))
        {
            var timeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            payload["timestamp"] = timeStamp.ToString();
            payload["sign"] = GenSign(timeStamp);
        }

        // 发起请求
        var url = _url;
        var request = await url.AsHttp().SetJsonBody(payload).PostAsync<LarkResultInfoDto>();

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
            var resultInfos = EnumHelper.GetTypedEnumItems<LarkResultErrCodeEnum>();
            var info = resultInfos.FirstOrDefault(e => e.Value.ConvertToInt() == result.Code);
            return BotResult.BadRequest("发送失败；" + (info?.Description ?? result.Msg));
        }
    }

    /// <summary>
    /// 生成签名
    /// 把 【timestamp + "\n" + 密钥】 作为 HmacSHA256 的密钥、空内容做摘要，结果 Base64 编码
    /// https://open.feishu.cn/document/client-docs/bot-v3/add-custom-bot
    /// </summary>
    /// <param name="timeStamp">Unix 秒级时间戳</param>
    /// <returns>签名</returns>
    private string GenSign(long timeStamp)
    {
        var stringToSign = timeStamp + "\n" + _secret;
        var hash = HmacHelper.HmacSha256(Encoding.UTF8.GetBytes(stringToSign), []);
        return Convert.ToBase64String(hash);
    }
}
