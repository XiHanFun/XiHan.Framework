// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Bot.DingTalk.Tests.Messaging;

/// <summary>
/// 钉钉加签算法测试
/// </summary>
/// <remarks>
/// DingTalkBot 私有的 Send 方法按钉钉协议加签：把 <c>timestamp + "\n" + secret</c> 当作待签名串，
/// 以 secret 为密钥做 HmacSHA256，Base64 后再 URL 编码挂到查询串上。
/// 该方法是私有的且只能在真实发送时触发，无法直接调用，因此这里按原文复刻这条算法链并用固定向量钉死：
/// 只要 HmacHelper、UrlEncode 或拼接顺序中的任何一环发生行为漂移，本组用例就会失败，
/// 而不是等到线上收到"sign not match"（errcode 310000）才发现。
/// 固定向量由 HMAC-SHA256 标准算法离线计算并与 openssl 交叉验证过。
/// </remarks>
public class DingTalkSignatureTests
{
    /// <summary>
    /// 固定密钥
    /// </summary>
    private const string SignSecret = "SECabcdefghijklmn";

    /// <summary>
    /// 固定时间戳（毫秒）
    /// </summary>
    private const long SignTimestamp = 1700000000000L;

    /// <summary>
    /// 固定密钥与时间戳必须得到固定签名
    /// </summary>
    /// <param name="secret">加签密钥</param>
    /// <param name="timestamp">毫秒时间戳</param>
    /// <param name="expected">期望的 Base64 签名</param>
    [Theory]
    [InlineData("SECabcdefghijklmn", 1700000000000L, "OwNgXdQnzpvRnLLEHzV841UHj9uG5TTlZKsPC2NG7tk=")]
    [InlineData("xihan-secret", 1600000000000L, "9uU1/DN/ZVYLZkkUs5uGay66kuIlJ8tgnmMjRhe7wi0=")]
    [InlineData("SEC27", 1700000000000L, "+03z0LCs/hp3oJ3HoLjFuMc6tk5lAGjqewkTfmKB+2M=")]
    public void RawSign_ForFixedInput_IsDeterministic(string secret, long timestamp, string expected)
    {
        Assert.Equal(expected, BuildRawSign(secret, timestamp));
    }

    /// <summary>
    /// 签名是 SHA256 摘要的 Base64 形式
    /// </summary>
    [Fact]
    public void RawSign_IsBase64OfSha256Digest()
    {
        var sign = BuildRawSign(SignSecret, SignTimestamp);

        Assert.Equal(44, sign.Length);
        Assert.Equal(32, Convert.FromBase64String(sign).Length);
    }

    /// <summary>
    /// 待签名串用的是换行符而不是平台换行
    /// </summary>
    /// <remarks>
    /// Windows 上若被"顺手"改成 <c>Environment.NewLine</c>，待签名串会变成 CRLF，
    /// 钉钉端算出的签名与本端不一致，直接被判为 sign not match。
    /// </remarks>
    [Fact]
    public void SignedPayload_UsesLineFeed_NotCarriageReturnLineFeed()
    {
        var lineFeed = HmacHelper.HmacSha256(SignSecret, SignTimestamp + "\n" + SignSecret);
        var carriageReturnLineFeed = HmacHelper.HmacSha256(SignSecret, SignTimestamp + "\r\n" + SignSecret);

        Assert.NotEqual(lineFeed, carriageReturnLineFeed);
        Assert.Equal("OwNgXdQnzpvRnLLEHzV841UHj9uG5TTlZKsPC2NG7tk=", lineFeed);
    }

    /// <summary>
    /// URL 编码把 Base64 的保留字符全部转义，且可无损还原
    /// </summary>
    /// <remarks>
    /// 签名要作为查询串参数传输，Base64 里的 <c>+</c>、<c>/</c>、<c>=</c> 不转义就会在服务端被解析错，
    /// 其中 <c>+</c> 会被当成空格，这是加签失败里最隐蔽的一种。
    /// </remarks>
    [Fact]
    public void UrlEncodedSign_EscapesBase64ReservedCharacters()
    {
        var raw = BuildRawSign("SEC27", SignTimestamp);

        Assert.Contains("+", raw);
        Assert.Contains("/", raw);
        Assert.Contains("=", raw);

        var encoded = raw.UrlEncode();

        Assert.DoesNotContain("+", encoded);
        Assert.DoesNotContain("/", encoded);
        Assert.DoesNotContain("=", encoded);
        Assert.Equal(raw, WebUtility.UrlDecode(encoded));
    }

    /// <summary>
    /// 只含填充符的签名同样被转义
    /// </summary>
    [Fact]
    public void UrlEncodedSign_EscapesPaddingCharacter()
    {
        var raw = BuildRawSign(SignSecret, SignTimestamp);

        var encoded = raw.UrlEncode();

        Assert.EndsWith("=", raw);
        Assert.DoesNotContain("=", encoded);
        Assert.Equal(raw, WebUtility.UrlDecode(encoded));
    }

    /// <summary>
    /// 时间戳变化则签名变化
    /// </summary>
    [Fact]
    public void Sign_ChangesWithTimestamp()
    {
        Assert.NotEqual(
            BuildRawSign(SignSecret, SignTimestamp),
            BuildRawSign(SignSecret, SignTimestamp + 1));
    }

    /// <summary>
    /// 密钥变化则签名变化
    /// </summary>
    [Fact]
    public void Sign_ChangesWithSecret()
    {
        Assert.NotEqual(
            BuildRawSign(SignSecret, SignTimestamp),
            BuildRawSign(SignSecret + "x", SignTimestamp));
    }

    /// <summary>
    /// 密钥为空串时加签直接抛参数异常
    /// </summary>
    /// <remarks>
    /// DingTalkOptions.Secret 默认就是空串，而提供者只校验 AccessToken，
    /// 也就是说"只配关键字不配加签"的机器人会走到这里抛异常而不是发出未签名请求，
    /// 这条断言用来固定该边界的真实行为（详见交付报告的疑似缺陷）。
    /// </remarks>
    [Fact]
    public void Sign_WhenSecretEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => HmacHelper.HmacSha256(string.Empty, SignTimestamp + "\n"));
    }

    /// <summary>
    /// 按 DingTalkBot.Send 的原文复刻加签串构造
    /// </summary>
    /// <param name="secret">加签密钥</param>
    /// <param name="timestamp">毫秒时间戳</param>
    /// <returns>Base64 签名</returns>
    private static string BuildRawSign(string secret, long timestamp)
    {
        var message = timestamp + "\n" + secret;
        return HmacHelper.HmacSha256(secret, message);
    }
}
