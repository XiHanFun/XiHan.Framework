// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 脱敏辅助类测试
/// </summary>
/// <remarks>
/// 脱敏结果会直接进日志与前端展示，形状一旦变化就是数据泄露或展示错乱，
/// 因此这里对每种业务脱敏都锁死具体输出串，而不是只断言「包含星号」。
/// 无参 <c>Mask()</c> 按长度分档走不同正则，六个分档全部覆盖。
/// </remarks>
public class MaskHelperTests
{
    /// <summary>
    /// 通用脱敏保留首尾指定位数
    /// </summary>
    [Theory]
    [InlineData("1234567890", 3, 4, "123***7890")]
    [InlineData("abcdefgh", 1, 1, "a******h")]
    [InlineData("abcdefgh", 0, 0, "********")]
    public void Mask_WithFrontAndEndCount_KeepsBoundaryCharacters(string input, int front, int end, string expected)
    {
        Assert.Equal(expected, input.Mask(front, end));
    }

    /// <summary>
    /// 自定义脱敏字符生效
    /// </summary>
    [Fact]
    public void Mask_WithCustomMaskChar_UsesGivenCharacter()
    {
        Assert.Equal("12●●●●7890", "1234567890".Mask(2, 4, '●'));
    }

    /// <summary>
    /// 脱敏字符传 null 时回落到默认星号
    /// </summary>
    [Fact]
    public void Mask_WhenMaskCharNull_FallsBackToAsterisk()
    {
        Assert.Equal("12****7890", "1234567890".Mask(2, 4, null));
    }

    /// <summary>
    /// 保留位数之和不小于串长时退化为按长度分档脱敏
    /// </summary>
    /// <remarks>
    /// "abc" 长度 3，落在默认分档（保留首字符 + 四个星号），因此结果是 "a****" 而不是原样返回。
    /// </remarks>
    [Fact]
    public void Mask_WhenKeptLengthExceedsInput_FallsBackToLengthBasedMask()
    {
        Assert.Equal("a****", "abc".Mask(2, 2));
    }

    /// <summary>
    /// 输入先被 Trim 再脱敏
    /// </summary>
    [Fact]
    public void Mask_TrimsInputBeforeMasking()
    {
        Assert.Equal("h***o", "  hello  ".Mask(1, 1));
    }

    /// <summary>
    /// 空白输入脱敏后为空串
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Mask_WithBlankInput_ReturnsEmpty(string input)
    {
        Assert.Equal(string.Empty, input.Mask(1, 1));
        Assert.Equal(string.Empty, input.Mask());
    }

    /// <summary>
    /// 按长度分档的无参脱敏：六个分档各自的保留位数
    /// </summary>
    [Theory]
    [InlineData("abcde", "a****")]
    [InlineData("abcdef", "a****f")]
    [InlineData("abcdefg", "a****fg")]
    [InlineData("abcdefgh", "ab****gh")]
    [InlineData("abcdefghi", "ab****ghi")]
    [InlineData("abcdefghij", "abc****hij")]
    [InlineData("abcdefghijk", "abc****hijk")]
    [InlineData("abcdefghijklmnop", "abc****mnop")]
    public void Mask_WithoutArguments_UsesLengthBasedBuckets(string input, string expected)
    {
        Assert.Equal(expected, input.Mask());
    }

    /// <summary>
    /// 手机号保留前三后四
    /// </summary>
    [Fact]
    public void MaskPhone_KeepsFirstThreeAndLastFour()
    {
        Assert.Equal("138****5678", MaskHelper.MaskPhone("13812345678"));
    }

    /// <summary>
    /// 长度不足七位的手机号整体脱敏
    /// </summary>
    [Fact]
    public void MaskPhone_WhenTooShort_MasksEverything()
    {
        Assert.Equal("******", MaskHelper.MaskPhone("123456"));
    }

    /// <summary>
    /// 身份证号保留前四后四
    /// </summary>
    [Fact]
    public void MaskIdCard_KeepsFirstFourAndLastFour()
    {
        Assert.Equal("1101**********001X", MaskHelper.MaskIdCard("11010119800101001X"));
    }

    /// <summary>
    /// 长度不足八位的身份证号整体脱敏
    /// </summary>
    [Fact]
    public void MaskIdCard_WhenTooShort_MasksEverything()
    {
        Assert.Equal("*******", MaskHelper.MaskIdCard("1101011"));
    }

    /// <summary>
    /// 银行卡号保留前四后四
    /// </summary>
    [Fact]
    public void MaskBankCard_KeepsFirstFourAndLastFour()
    {
        Assert.Equal("6222***********3445", MaskHelper.MaskBankCard("6222020200112233445"));
    }

    /// <summary>
    /// 中文姓名按字数分档脱敏
    /// </summary>
    [Theory]
    [InlineData("张", "张")]
    [InlineData("张三", "张*")]
    [InlineData("诸葛亮", "诸*亮")]
    [InlineData("欧阳娜娜", "欧**娜")]
    [InlineData("", "")]
    public void MaskChineseName_MasksByNameLength(string name, string expected)
    {
        Assert.Equal(expected, MaskHelper.MaskChineseName(name));
    }

    /// <summary>
    /// 地址保留前六后二
    /// </summary>
    [Fact]
    public void MaskAddress_KeepsFirstSixAndLastTwo()
    {
        Assert.Equal("北京市朝阳区***1号", MaskHelper.MaskAddress("北京市朝阳区建国路1号"));
    }

    /// <summary>
    /// 长度不超过八个字的地址原样返回
    /// </summary>
    [Theory]
    [InlineData("北京市朝阳区")]
    [InlineData("北京市朝阳区建国")]
    [InlineData("")]
    public void MaskAddress_WhenShort_ReturnsOriginal(string address)
    {
        Assert.Equal(address, MaskHelper.MaskAddress(address));
    }

    /// <summary>
    /// 密码整体替换为等长星号
    /// </summary>
    [Theory]
    [InlineData("P@ssw0rd", "********")]
    [InlineData("a", "*")]
    [InlineData("", "")]
    public void MaskPassword_ReplacesEveryCharacter(string password, string expected)
    {
        Assert.Equal(expected, MaskHelper.MaskPassword(password));
    }

    /// <summary>
    /// 车牌号保留前二后一
    /// </summary>
    [Fact]
    public void MaskLicensePlate_KeepsFirstTwoAndLastOne()
    {
        Assert.Equal("京A****5", MaskHelper.MaskLicensePlate("京A12345"));
    }

    /// <summary>
    /// 单字符车牌与空串原样返回
    /// </summary>
    [Theory]
    [InlineData("京")]
    [InlineData("")]
    public void MaskLicensePlate_WhenTooShort_ReturnsOriginal(string plate)
    {
        Assert.Equal(plate, MaskHelper.MaskLicensePlate(plate));
    }

    /// <summary>
    /// URL 中的敏感查询参数值被整体替换
    /// </summary>
    [Theory]
    [InlineData("https://a.com/x?token=abc123&user=bob", "https://a.com/x?token=******&user=bob")]
    [InlineData("https://a.com/x?password=p%40ss", "https://a.com/x?password=******")]
    [InlineData("https://a.com/x?pwd=1234&secret=s3cr3t", "https://a.com/x?pwd=******&secret=******")]
    public void MaskUrlParams_ReplacesSensitiveValues(string url, string expected)
    {
        Assert.Equal(expected, MaskHelper.MaskUrlParams(url));
    }

    /// <summary>
    /// 不含敏感参数的 URL 与空串原样返回
    /// </summary>
    [Theory]
    [InlineData("https://a.com/x?user=bob")]
    [InlineData("")]
    public void MaskUrlParams_WithoutSensitiveKeys_ReturnsOriginal(string url)
    {
        Assert.Equal(url, MaskHelper.MaskUrlParams(url));
    }

    /// <summary>
    /// 大小写不敏感的正则模式按字符逐个展开
    /// </summary>
    [Theory]
    [InlineData("Ab", "[aA][bB]")]
    [InlineData("xy", "[xX][yY]")]
    [InlineData("", "")]
    public void GenerateCaseInsensitivePattern_ExpandsEachCharacter(string word, string expected)
    {
        Assert.Equal(expected, MaskHelper.GenerateCaseInsensitivePattern(word));
    }

    /// <summary>
    /// 非邮箱格式的输入原样返回
    /// </summary>
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.com")]
    [InlineData("")]
    public void MaskEmail_WhenNotAnEmail_ReturnsOriginal(string email)
    {
        Assert.Equal(email, MaskHelper.MaskEmail(email));
    }

    /// <summary>
    /// 邮箱脱敏必须保留域名与后缀，只遮蔽用户名与域名主体的中段
    /// </summary>
    /// <remarks>
    /// 【已知红灯 / 疑似缺陷】实现依赖 <c>RegexHelper.EmailRegex()</c> 的第 1/2/3 个捕获组取
    /// 用户名、域名、后缀，但该正则**没有任何捕获组**，三个 Groups 取到的都是空串，
    /// 于是任何合法邮箱都被脱敏成 <c>"@."</c>——原始信息全丢，也不符合方法自身的文档示例。
    /// 这里断言的是「任何正确实现都必须满足」的不变量（保留 @ 与顶级域、含遮蔽符、不得退化成 "@."），
    /// 不猜具体形状；缺陷已上报由主控裁决。
    /// </remarks>
    [Fact]
    public void MaskEmail_KeepsDomainAndSuffix()
    {
        var masked = MaskHelper.MaskEmail("test@example.com");

        Assert.NotEqual("@.", masked);
        Assert.Contains("@", masked);
        Assert.Contains("*", masked);
        Assert.EndsWith("com", masked);
    }
}
