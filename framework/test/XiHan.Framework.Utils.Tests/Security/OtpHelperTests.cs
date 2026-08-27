// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Converters;
using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 一次性密码辅助类测试
/// </summary>
/// <remarks>
/// HOTP 是纯函数（密钥 + 计数器 → 定长数字串），因此直接锁死 RFC 4226 附录 D 的官方向量：
/// 密钥为 ASCII "12345678901234567890"，Base32 编码为 GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ。
/// 只要有人把内部摘要从 HMAC-SHA1 换掉、把大端计数器写成小端、或改了动态截取逻辑，
/// 生成的验证码就会与 Google Authenticator 等标准实现对不上，这些向量会立刻变红。
/// TOTP 依赖真实时钟，只做「自洽 + 时间窗口换算」验证，不锁具体数值。
/// </remarks>
public class OtpHelperTests
{
    /// <summary>
    /// RFC 4226 附录 D 的共享密钥（ASCII）
    /// </summary>
    private const string RfcSecretAscii = "12345678901234567890";

    /// <summary>
    /// 上述密钥的 Base32 编码
    /// </summary>
    private const string RfcSecretBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    /// <summary>
    /// 本仓 Base32 编码器对 RFC 密钥的输出与公开值一致
    /// </summary>
    /// <remarks>
    /// 这一条把「向量常量」与「实际编码器」对齐，避免下面的 HOTP 向量因为 Base32 侧的问题被误判。
    /// </remarks>
    [Fact]
    public void Base32Encoding_OfRfcSecret_MatchesPublishedValue()
    {
        Assert.Equal(RfcSecretBase32, Base32.Encode(Encoding.ASCII.GetBytes(RfcSecretAscii)));
    }

    /// <summary>
    /// HOTP 六位码固定向量（RFC 4226 附录 D）
    /// </summary>
    [Theory]
    [InlineData(0, "755224")]
    [InlineData(1, "287082")]
    [InlineData(2, "359152")]
    [InlineData(3, "969429")]
    [InlineData(4, "338314")]
    [InlineData(5, "254676")]
    [InlineData(6, "287922")]
    [InlineData(7, "162583")]
    [InlineData(8, "399871")]
    [InlineData(9, "520489")]
    public void GenerateHotp_ForRfcVectors_MatchesPublishedCodes(int counter, string expected)
    {
        Assert.Equal(expected, OtpHelper.GenerateHotp(RfcSecretBase32, counter));
    }

    /// <summary>
    /// HOTP 八位码固定向量（RFC 4226 附录 D 中 count = 0 的截取值 0x4C93CF18 取模 10^8）
    /// </summary>
    [Fact]
    public void GenerateHotp_WithEightDigits_MatchesPublishedTruncation()
    {
        Assert.Equal("84755224", OtpHelper.GenerateHotp(RfcSecretBase32, 0, 8));
    }

    /// <summary>
    /// Base64 编码的同一密钥产出同样的 HOTP
    /// </summary>
    [Fact]
    public void GenerateHotp_WithBase64Secret_MatchesBase32Result()
    {
        var base64Secret = Convert.ToBase64String(Encoding.ASCII.GetBytes(RfcSecretAscii));

        Assert.Equal("755224", OtpHelper.GenerateHotp(base64Secret, 0, 6, useBase32: false));
    }

    /// <summary>
    /// 生成的验证码位数与请求位数一致，且左侧补零
    /// </summary>
    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public void GenerateHotp_PadsToRequestedDigits(int digits)
    {
        for (long counter = 0; counter < 20; counter++)
        {
            var otp = OtpHelper.GenerateHotp(RfcSecretBase32, counter, digits);

            Assert.Equal(digits, otp.Length);
            Assert.Matches("^[0-9]+$", otp);
        }
    }

    /// <summary>
    /// 密钥为空时抛空引用参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateHotp_WhenSecretBlank_ThrowsArgumentNullException(string secretKey)
    {
        Assert.Throws<ArgumentNullException>(() => { _ = OtpHelper.GenerateHotp(secretKey, 0); });
        Assert.Throws<ArgumentNullException>(() => { _ = OtpHelper.GenerateTotp(secretKey); });
    }

    /// <summary>
    /// HOTP 验证：计数器一致才通过
    /// </summary>
    [Fact]
    public void VerifyHotp_RequiresMatchingCounter()
    {
        Assert.True(OtpHelper.VerifyHotp(RfcSecretBase32, "755224", 0));
        Assert.False(OtpHelper.VerifyHotp(RfcSecretBase32, "755224", 1));
        Assert.False(OtpHelper.VerifyHotp(RfcSecretBase32, "000000", 0));
    }

    /// <summary>
    /// HOTP 验证：密钥或验证码为空时直接判否，不抛异常
    /// </summary>
    [Fact]
    public void VerifyHotp_WithBlankInput_ReturnsFalse()
    {
        Assert.False(OtpHelper.VerifyHotp(string.Empty, "755224", 0));
        Assert.False(OtpHelper.VerifyHotp(RfcSecretBase32, string.Empty, 0));
    }

    /// <summary>
    /// TOTP 生成后立即验证必定通过
    /// </summary>
    [Fact]
    public void VerifyTotp_ForFreshlyGeneratedCode_ReturnsTrue()
    {
        var otp = OtpHelper.GenerateTotp(RfcSecretBase32);

        Assert.Equal(6, otp.Length);
        Assert.True(OtpHelper.VerifyTotp(RfcSecretBase32, otp));
    }

    /// <summary>
    /// TOTP 验证：换密钥或换验证码都不通过
    /// </summary>
    [Fact]
    public void VerifyTotp_WithWrongSecretOrCode_ReturnsFalse()
    {
        var otp = OtpHelper.GenerateTotp(RfcSecretBase32);
        var otherSecret = Base32.Encode(Encoding.ASCII.GetBytes("09876543210987654321"));

        Assert.False(OtpHelper.VerifyTotp(otherSecret, otp));
        Assert.False(OtpHelper.VerifyTotp(RfcSecretBase32, string.Empty));
    }

    /// <summary>
    /// TOTP 与当前时间窗口的 HOTP 是同一个值
    /// </summary>
    /// <remarks>
    /// 时间窗口可能在两次调用之间翻页，因此允许命中当前窗口或相邻窗口，
    /// 这正是 <see cref="OtpHelper.VerifyTotp"/> 默认 allowedSkew = 1 想覆盖的情形。
    /// </remarks>
    [Fact]
    public void GenerateTotp_EqualsHotpOfCurrentTimeWindow()
    {
        var otp = OtpHelper.GenerateTotp(RfcSecretBase32);
        var window = OtpHelper.GetCurrentTimeWindow();

        var candidates = new[]
        {
            OtpHelper.GenerateHotp(RfcSecretBase32, window - 1),
            OtpHelper.GenerateHotp(RfcSecretBase32, window),
            OtpHelper.GenerateHotp(RfcSecretBase32, window + 1)
        };

        Assert.Contains(otp, candidates);
    }

    /// <summary>
    /// 时间窗口按步长切分 Unix 秒
    /// </summary>
    [Theory]
    [InlineData(0, 30, 0)]
    [InlineData(29, 30, 0)]
    [InlineData(30, 30, 1)]
    [InlineData(59, 30, 1)]
    [InlineData(60, 30, 2)]
    [InlineData(60, 60, 1)]
    public void GetTimeWindow_DividesUnixSecondsByStep(int unixSeconds, int step, int expected)
    {
        Assert.Equal(expected, OtpHelper.GetTimeWindow(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), step));
    }

    /// <summary>
    /// 指定时间的 Unix 秒换算
    /// </summary>
    [Fact]
    public void GetUnixTimeSeconds_MatchesDateTimeOffset()
    {
        var moment = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

        Assert.Equal(moment.ToUnixTimeSeconds(), OtpHelper.GetUnixTimeSeconds(moment));
    }

    /// <summary>
    /// 剩余秒数落在 (0, step] 区间
    /// </summary>
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    public void GetRemainingSeconds_IsWithinStep(int step)
    {
        var remaining = OtpHelper.GetRemainingSeconds(step);

        Assert.InRange(remaining, 1, step);
    }

    /// <summary>
    /// 当前时间窗口的起止时间正好相差一个步长，且当前时间落在窗口内
    /// </summary>
    [Fact]
    public void GetCurrentWindowBoundaries_SpanExactlyOneStep()
    {
        var windowBefore = OtpHelper.GetCurrentTimeWindow();
        var start = OtpHelper.GetCurrentWindowStartTime();
        var end = OtpHelper.GetCurrentWindowEndTime();
        var windowAfter = OtpHelper.GetCurrentTimeWindow();

        // 起止时间是分两次读时钟算出来的，窗口正好翻页时两者不属于同一窗口，此时不做断言
        Assert.SkipUnless(windowBefore == windowAfter, "时间窗口在取值过程中翻页，跳过本次验证。");

        Assert.Equal(TimeSpan.FromSeconds(30), end - start);
        Assert.Equal(windowBefore, start.ToUnixTimeSeconds() / 30);
    }

    /// <summary>
    /// 当前 Unix 秒与系统时钟一致（允许若干秒误差）
    /// </summary>
    [Fact]
    public void GetCurrentUnixTimeSeconds_TracksSystemClock()
    {
        var delta = Math.Abs(OtpHelper.GetCurrentUnixTimeSeconds() - DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        Assert.True(delta <= 5);
    }

    /// <summary>
    /// 批量生成覆盖当前窗口前后各若干个，键是连续的时间窗口
    /// </summary>
    [Fact]
    public void GenerateMultipleTotps_ReturnsConsecutiveWindows()
    {
        var result = OtpHelper.GenerateMultipleTotps(RfcSecretBase32);

        Assert.Equal(5, result.Count);

        var windows = result.Keys.OrderBy(k => k).ToList();
        for (var i = 1; i < windows.Count; i++)
        {
            Assert.Equal(windows[i - 1] + 1, windows[i]);
        }

        Assert.All(result.Values, otp => Assert.Equal(6, otp.Length));
        Assert.All(windows, window => Assert.Equal(result[window], OtpHelper.GenerateHotp(RfcSecretBase32, window)));
    }

    /// <summary>
    /// 完整信息元组各字段自洽
    /// </summary>
    [Fact]
    public void GetTotpWithInfo_ReturnsSelfConsistentTuple()
    {
        var windowBefore = OtpHelper.GetCurrentTimeWindow();
        var info = OtpHelper.GetTotpWithInfo(RfcSecretBase32);
        var windowAfter = OtpHelper.GetCurrentTimeWindow();

        Assert.Equal(6, info.Otp.Length);
        Assert.InRange(info.RemainingSeconds, 1, 30);

        // 元组各字段是分多次读时钟算出来的，窗口正好翻页时彼此不属于同一窗口，此时不做一致性断言
        Assert.SkipUnless(windowBefore == windowAfter, "时间窗口在取值过程中翻页，跳过本次验证。");

        Assert.Equal(TimeSpan.FromSeconds(30), info.WindowEnd - info.WindowStart);
        Assert.Equal(info.WindowStart.ToUnixTimeSeconds() / 30, info.TimeWindow);
    }

    /// <summary>
    /// 生成的随机密钥长度与编码符合约定
    /// </summary>
    [Fact]
    public void GenerateSecretKey_ProducesRequestedEntropyInRequestedEncoding()
    {
        var base32Key = OtpHelper.GenerateSecretKey();
        var base64Key = OtpHelper.GenerateSecretKey(20, useBase32: false);

        // 20 字节 = 160 位，Base32 无填充下正好 32 个字符
        Assert.Equal(32, base32Key.Length);
        Assert.Matches("^[A-Z2-7]+$", base32Key);
        Assert.Equal(20, Base32.Decode(base32Key).Length);
        Assert.Equal(20, Convert.FromBase64String(base64Key).Length);
    }

    /// <summary>
    /// 每次生成的随机密钥都不同
    /// </summary>
    [Fact]
    public void GenerateSecretKey_ProducesDistinctValues()
    {
        var keys = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            keys.Add(OtpHelper.GenerateSecretKey());
        }

        Assert.Equal(50, keys.Count);
    }

    /// <summary>
    /// 生成的密钥可以直接用于 TOTP 生成与验证
    /// </summary>
    [Fact]
    public void GenerateSecretKey_IsUsableForTotp()
    {
        var secretKey = OtpHelper.GenerateSecretKey();
        var otp = OtpHelper.GenerateTotp(secretKey);

        Assert.True(OtpHelper.VerifyTotp(secretKey, otp));
    }

    /// <summary>
    /// TOTP 二维码 URI 的完整形状
    /// </summary>
    /// <remarks>
    /// 这个串会被 Google Authenticator 之类的客户端直接解析，参数名、顺序与转义规则都不能改。
    /// </remarks>
    [Fact]
    public void GenerateTotpUri_ProducesOtpAuthUri()
    {
        var uri = OtpHelper.GenerateTotpUri("ABCDEFGH", "user@example.com", "My App");

        Assert.Equal(
            "otpauth://totp/My%20App:user%40example.com?secret=ABCDEFGH&issuer=My%20App&digits=6&period=30&algorithm=SHA1",
            uri);
    }

    /// <summary>
    /// HOTP 二维码 URI 的完整形状
    /// </summary>
    [Fact]
    public void GenerateHotpUri_ProducesOtpAuthUri()
    {
        var uri = OtpHelper.GenerateHotpUri("ABCDEFGH", "user@example.com", "My App", 7);

        Assert.Equal(
            "otpauth://hotp/My%20App:user%40example.com?secret=ABCDEFGH&issuer=My%20App&digits=6&counter=7&algorithm=SHA1",
            uri);
    }

    /// <summary>
    /// URI 生成的必填参数缺失时抛空引用参数异常
    /// </summary>
    [Fact]
    public void GenerateTotpUri_WhenRequiredArgumentBlank_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = OtpHelper.GenerateTotpUri(string.Empty, "user", "issuer"); });
        Assert.Throws<ArgumentNullException>(() => { _ = OtpHelper.GenerateTotpUri("ABCDEFGH", string.Empty, "issuer"); });
        Assert.Throws<ArgumentNullException>(() => { _ = OtpHelper.GenerateTotpUri("ABCDEFGH", "user", string.Empty); });
        Assert.Throws<ArgumentNullException>(() => { _ = OtpHelper.GenerateHotpUri(string.Empty, "user", "issuer"); });
        Assert.Throws<ArgumentNullException>(() => { _ = OtpHelper.GenerateHotpUri("ABCDEFGH", string.Empty, "issuer"); });
        Assert.Throws<ArgumentNullException>(() => { _ = OtpHelper.GenerateHotpUri("ABCDEFGH", "user", string.Empty); });
    }
}
