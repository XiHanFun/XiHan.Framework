// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Bot.DingTalk.Messaging;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.DingTalk.Tests.Messaging;

/// <summary>
/// 钉钉自定义机器人推送客户端测试
/// </summary>
/// <remarks>
/// 五个发送方法都会真的 POST 到钉钉网关，CI 不允许出网，所以这里只覆盖构造期与公共方法形状：
/// 1）Webhook 地址拼接 —— <c>WebHookUrl + "?access_token=" + AccessToken</c>，问号与 access_token 参数名由钉钉协议规定；
/// 2）关键字前缀 —— 配了关键字才拼，且必须补一个换行，否则关键字会和正文粘在一起；
/// 3）公共发送方法的数量与返回类型。
/// 前两项落在 private 字段上，只能反射进入；反射目标缺失时用 Assert.SkipUnless 显式跳过，
/// 避免把"字段重命名"误报成"拼接算错"。真实推送见文末的 Skip 用例。
/// </remarks>
public class DingTalkBotTests
{
    /// <summary>
    /// 未配置 Webhook 时使用钉钉官方网关并挂上访问令牌
    /// </summary>
    [Fact]
    public void Ctor_WhenWebHookUrlNotConfigured_ComposesOfficialEndpointWithAccessToken()
    {
        var bot = new DingTalkBot(new DingTalkOptions { AccessToken = "abc-token" });

        Assert.Equal(
            "https://oapi.dingtalk.com/robot/send?access_token=abc-token",
            (string?)GetPrivateField(bot, "_url"));
    }

    /// <summary>
    /// 自建网关地址同样参与拼接
    /// </summary>
    [Fact]
    public void Ctor_WhenCustomWebHookUrl_ComposesWithThatEndpoint()
    {
        var bot = new DingTalkBot(new DingTalkOptions
        {
            WebHookUrl = "https://proxy.invalid/robot/send",
            AccessToken = "abc-token"
        });

        Assert.Equal(
            "https://proxy.invalid/robot/send?access_token=abc-token",
            (string?)GetPrivateField(bot, "_url"));
    }

    /// <summary>
    /// 未配置关键字时不产生任何前缀
    /// </summary>
    [Fact]
    public void Ctor_WhenKeyWordNull_KeepsPrefixNull()
    {
        var bot = new DingTalkBot(new DingTalkOptions { AccessToken = "abc-token" });

        Assert.Null(GetPrivateField(bot, "_keyWord"));
    }

    /// <summary>
    /// 配置关键字时前缀补一个换行
    /// </summary>
    /// <remarks>
    /// 钉钉的关键字安全设置只要求正文包含关键字，但前缀不补换行会把关键字和正文首行粘成一句话，
    /// markdown 消息尤其会被吞掉标题层级，所以这里锁死换行。
    /// </remarks>
    [Fact]
    public void Ctor_WhenKeyWordConfigured_AppendsLineFeedToPrefix()
    {
        var bot = new DingTalkBot(new DingTalkOptions
        {
            AccessToken = "abc-token",
            KeyWord = "监控告警"
        });

        Assert.Equal("监控告警\n", (string?)GetPrivateField(bot, "_keyWord"));
    }

    /// <summary>
    /// 关键字为空串时仍按"已配置"处理并补换行
    /// </summary>
    /// <remarks>
    /// 构造函数判的是 null 而不是空白，空串配置项会得到一个纯换行前缀；
    /// 这条断言用来固定"只有 null 才算未配置"的判定口径。
    /// </remarks>
    [Fact]
    public void Ctor_WhenKeyWordEmpty_StillTreatedAsConfigured()
    {
        var bot = new DingTalkBot(new DingTalkOptions
        {
            AccessToken = "abc-token",
            KeyWord = string.Empty
        });

        Assert.Equal("\n", (string?)GetPrivateField(bot, "_keyWord"));
    }

    /// <summary>
    /// 加签密钥被原样保留，不做裁剪或转码
    /// </summary>
    [Fact]
    public void Ctor_KeepsSecretVerbatim()
    {
        var bot = new DingTalkBot(new DingTalkOptions
        {
            AccessToken = "abc-token",
            Secret = "SECabcdefghijklmn"
        });

        Assert.Equal("SECabcdefghijklmn", (string?)GetPrivateField(bot, "_secret"));
    }

    /// <summary>
    /// 客户端只暴露五个发送方法，且全部返回 <see cref="BotResult"/> 任务
    /// </summary>
    /// <remarks>
    /// 五个方法一一对应钉钉支持的五种 msgtype；发送入口 Send 必须保持私有，
    /// 否则调用方可以绕过关键字前缀与加签逻辑直接投递任意报文。
    /// </remarks>
    [Fact]
    public void PublicMethods_AreFiveMessageSendersReturningBotResult()
    {
        var methods = typeof(DingTalkBot)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .ToList();

        Assert.Equal(5, methods.Count);
        Assert.All(methods, method => Assert.Equal(typeof(Task<BotResult>), method.ReturnType));

        var names = methods.Select(method => method.Name).ToList();

        Assert.Contains("TextMessage", names);
        Assert.Contains("LinkMessage", names);
        Assert.Contains("MarkdownMessage", names);
        Assert.Contains("ActionCardMessage", names);
        Assert.Contains("FeedCardMessage", names);
        Assert.DoesNotContain("Send", names);
    }

    /// <summary>
    /// 每个发送方法的最后一个参数都是可省略的取消令牌
    /// </summary>
    [Fact]
    public void PublicMethods_AcceptOptionalCancellationToken()
    {
        var methods = typeof(DingTalkBot)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        Assert.All(methods, method =>
        {
            var parameters = method.GetParameters();
            var last = parameters[^1];

            Assert.Equal(typeof(CancellationToken), last.ParameterType);
            Assert.True(last.HasDefaultValue);
        });
    }

    /// <summary>
    /// 真实推送到钉钉自定义机器人的链路不在单元测试覆盖范围
    /// </summary>
    [Fact]
    public void TextMessage_AgainstRealWebhook_RequiresCredentials()
    {
        Assert.Skip("需要真实钉钉自定义机器人 access_token、加签密钥与外网访问 oapi.dingtalk.com，CI 不具备。");
    }

    /// <summary>
    /// 反射读取私有字段
    /// </summary>
    /// <param name="bot">机器人实例</param>
    /// <param name="fieldName">字段名</param>
    /// <returns>字段值</returns>
    private static object? GetPrivateField(DingTalkBot bot, string fieldName)
    {
        var field = typeof(DingTalkBot).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.SkipUnless(field is not null, $"DingTalkBot 私有字段 {fieldName} 已重命名或移除，跳过该项验证。");

        return field!.GetValue(bot);
    }
}
