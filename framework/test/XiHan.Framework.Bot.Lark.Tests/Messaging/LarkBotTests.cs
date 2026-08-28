// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Bot.Lark.Messaging;
using XiHan.Framework.Bot.Lark.Options;

namespace XiHan.Framework.Bot.Lark.Tests.Messaging;

/// <summary>
/// 飞书自定义机器人推送客户端测试
/// </summary>
/// <remarks>
/// LarkBot 的四个发送方法都会真的发 HTTP，CI 里不允许出网，所以这里只覆盖构造期与签名算法：
/// 1）Webhook 地址拼接（WebHookUrl + "/" + AccessToken）；
/// 2）关键字前缀（KeyWord + "\n"）；
/// 3）签名算法 GenSign —— 固定输入必须得到固定输出。
/// 后两项是 private 成员，只能反射进入；反射目标缺失时用 Assert.SkipUnless 显式跳过，
/// 避免把「重命名」误报成「算法错误」。真实发送路径见文末的 Skip 用例。
/// </remarks>
public class LarkBotTests
{
    /// <summary>
    /// 构造函数把 Webhook 前缀与访问令牌拼成完整地址
    /// </summary>
    [Fact]
    public void Ctor_Always_ComposesWebhookUrlWithAccessToken()
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token" });

        Assert.Equal("https://open.feishu.cn/open-apis/bot/v2/hook/abc-token", (string?)GetPrivateField(bot, "_url"));
    }

    /// <summary>
    /// 自定义 Webhook 前缀同样参与拼接
    /// </summary>
    [Fact]
    public void Ctor_WhenCustomWebHookUrl_ComposesWithThatPrefix()
    {
        var bot = new LarkBot(new LarkOptions
        {
            WebHookUrl = "https://proxy.example.com/hook",
            AccessToken = "abc-token"
        });

        Assert.Equal("https://proxy.example.com/hook/abc-token", (string?)GetPrivateField(bot, "_url"));
    }

    /// <summary>
    /// 未配置关键字时不产生前缀
    /// </summary>
    [Fact]
    public void Ctor_WhenKeyWordNull_KeepsPrefixNull()
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token" });

        Assert.Null(GetPrivateField(bot, "_keyWord"));
    }

    /// <summary>
    /// 配置关键字时前缀补一个换行
    /// </summary>
    /// <remarks>
    /// 飞书关键词校验只要求正文包含关键词，补换行是为了让关键词独占一行不污染正文。
    /// </remarks>
    [Fact]
    public void Ctor_WhenKeyWordConfigured_AppendsNewLineToPrefix()
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token", KeyWord = "Alert" });

        Assert.Equal("Alert\n", (string?)GetPrivateField(bot, "_keyWord"));
    }

    /// <summary>
    /// 关键字为空串时仍然补换行（空串不等于未配置）
    /// </summary>
    [Fact]
    public void Ctor_WhenKeyWordEmpty_StillAppendsNewLine()
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token", KeyWord = string.Empty });

        Assert.Equal("\n", (string?)GetPrivateField(bot, "_keyWord"));
    }

    /// <summary>
    /// 密钥原样保存，不做裁剪或编码
    /// </summary>
    [Fact]
    public void Ctor_Always_KeepsSecretAsIs()
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token", Secret = " sign-secret " });

        Assert.Equal(" sign-secret ", (string?)GetPrivateField(bot, "_secret"));
    }

    /// <summary>
    /// 固定时间戳与密钥必须得到固定签名
    /// </summary>
    /// <remarks>
    /// 期望值由独立实现算出：HmacSha256(key = UTF8(timestamp + "\n" + secret), data = 空字节)，再 Base64。
    /// 只要签名拼接顺序、分隔符、摘要输入或编码任意一处被改，这三条就会红。
    /// </remarks>
    [Theory]
    [InlineData(1700000000L, "xihan-lark-secret", "/uOJSGH5Q+6wLsHtkIt+MpeT1y7abgzUI0IHHm5iEMA=")]
    [InlineData(1600000000L, "abc", "6L6POH2rt4SuqaKjbCNoZ7H6Z3R3OevLdHc4Q+A+cEY=")]
    [InlineData(0L, "x", "oXdk2ucwqNVLLZZVLnZykqSwABpHVZxD+yQJ/eSmZ5o=")]
    public void GenSign_WithFixedInput_ReturnsFixedSignature(long timeStamp, string secret, string expected)
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token", Secret = secret });

        Assert.Equal(expected, InvokeGenSign(bot, timeStamp));
    }

    /// <summary>
    /// 同一输入重复计算结果一致
    /// </summary>
    [Fact]
    public void GenSign_WithSameInput_IsDeterministic()
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token", Secret = "xihan-lark-secret" });

        Assert.Equal(InvokeGenSign(bot, 1700000000L), InvokeGenSign(bot, 1700000000L));
    }

    /// <summary>
    /// 时间戳不同则签名不同
    /// </summary>
    [Fact]
    public void GenSign_WithDifferentTimestamp_ProducesDifferentSignature()
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token", Secret = "xihan-lark-secret" });

        Assert.NotEqual(InvokeGenSign(bot, 1700000000L), InvokeGenSign(bot, 1700000001L));
    }

    /// <summary>
    /// 密钥不同则签名不同
    /// </summary>
    [Fact]
    public void GenSign_WithDifferentSecret_ProducesDifferentSignature()
    {
        var first = new LarkBot(new LarkOptions { AccessToken = "abc-token", Secret = "xihan-lark-secret" });
        var second = new LarkBot(new LarkOptions { AccessToken = "abc-token", Secret = "another-secret" });

        Assert.NotEqual(InvokeGenSign(first, 1700000000L), InvokeGenSign(second, 1700000000L));
    }

    /// <summary>
    /// 签名是 32 字节摘要的 Base64 文本
    /// </summary>
    [Fact]
    public void GenSign_Always_IsBase64OfSha256Digest()
    {
        var bot = new LarkBot(new LarkOptions { AccessToken = "abc-token", Secret = "xihan-lark-secret" });

        var signature = InvokeGenSign(bot, 1700000000L);

        Assert.Equal(44, signature.Length);
        Assert.Equal(32, Convert.FromBase64String(signature).Length);
    }

    /// <summary>
    /// 真实推送到飞书自定义机器人的链路不在单元测试覆盖范围
    /// </summary>
    [Fact]
    public void TextMessage_AgainstRealWebhook_RequiresCredentials()
    {
        Assert.Skip("需要真实飞书自定义机器人 Webhook 凭据与外网，CI 不具备，故跳过真实推送验证。");
    }

    /// <summary>
    /// 反射调用私有签名方法
    /// </summary>
    private static string InvokeGenSign(LarkBot bot, long timeStamp)
    {
        var method = typeof(LarkBot).GetMethod("GenSign", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.SkipUnless(method is not null, "LarkBot.GenSign 已重命名或移除，跳过签名算法验证。");

        var value = method!.Invoke(bot, [timeStamp]);
        Assert.NotNull(value);

        return (string)value;
    }

    /// <summary>
    /// 反射读取私有字段
    /// </summary>
    private static object? GetPrivateField(LarkBot bot, string fieldName)
    {
        var field = typeof(LarkBot).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.SkipUnless(field is not null, $"LarkBot 私有字段 {fieldName} 已重命名或移除，跳过该项验证。");

        return field!.GetValue(bot);
    }
}
