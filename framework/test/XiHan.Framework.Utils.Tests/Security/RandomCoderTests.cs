// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Constants;
using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 加密安全随机码生成器测试
/// </summary>
/// <remarks>
/// 这些方法的输出会直接当验证码、临时密码、密钥片段用，两条契约必须守住：
/// 一是字符只能来自约定的字符源（否则短信通道或前端校验会挂），
/// 二是长度必须精确等于请求值（截断会显著削弱强度）。
/// 随机性本身无法用单测证明，这里只做「多次取值不应全部相同」的下限检查。
/// </remarks>
public class RandomCoderTests
{
    /// <summary>
    /// 数字随机码只含数字且长度精确
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(64)]
    public void GetNumber_ProducesDigitsOfRequestedLength(int length)
    {
        var code = RandomCoder.GetNumber(length);

        Assert.Equal(length, code.Length);
        Assert.All(code, c => Assert.Contains(c, DefaultConsts.Digits));
    }

    /// <summary>
    /// 不传长度时默认六位
    /// </summary>
    [Fact]
    public void GetNumber_ByDefault_ProducesSixDigits()
    {
        Assert.Equal(6, RandomCoder.GetNumber().Length);
    }

    /// <summary>
    /// 自定义字符源时只从该源取值
    /// </summary>
    [Fact]
    public void GetNumber_WithCustomSource_OnlyUsesThatSource()
    {
        var code = RandomCoder.GetNumber(20, "07");

        Assert.Equal(20, code.Length);
        Assert.All(code, c => Assert.Contains(c, "07"));
    }

    /// <summary>
    /// 字母随机码只含大小写字母
    /// </summary>
    [Fact]
    public void GetLetter_ProducesLettersOnly()
    {
        var code = RandomCoder.GetLetter(64);

        Assert.Equal(64, code.Length);
        Assert.All(code, c => Assert.True(char.IsAsciiLetter(c)));
    }

    /// <summary>
    /// 大写字母随机码只含大写字母
    /// </summary>
    [Fact]
    public void GetUpperLetter_ProducesUppercaseOnly()
    {
        var code = RandomCoder.GetUpperLetter(64);

        Assert.All(code, c => Assert.Contains(c, DefaultConsts.UppercaseLetters));
    }

    /// <summary>
    /// 小写字母随机码只含小写字母
    /// </summary>
    [Fact]
    public void GetLowerLetter_ProducesLowercaseOnly()
    {
        var code = RandomCoder.GetLowerLetter(64);

        Assert.All(code, c => Assert.Contains(c, DefaultConsts.LowercaseLetters));
    }

    /// <summary>
    /// 自定义字符源会按大小写方法自身的语义归一化
    /// </summary>
    [Fact]
    public void UpperAndLowerLetter_NormalizeCustomSourceCasing()
    {
        Assert.All(RandomCoder.GetUpperLetter(32, "abc"), c => Assert.Contains(c, "ABC"));
        Assert.All(RandomCoder.GetLowerLetter(32, "ABC"), c => Assert.Contains(c, "abc"));
    }

    /// <summary>
    /// 字母数字随机码只含字母与数字
    /// </summary>
    [Fact]
    public void GetNumberOrLetter_ProducesAlphanumericOnly()
    {
        var code = RandomCoder.GetNumberOrLetter(64);

        Assert.All(code, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
    }

    /// <summary>
    /// 特殊符号随机码只含不含引号的特殊符号集
    /// </summary>
    [Fact]
    public void GetSpecialChars_ProducesQuoteFreeSpecialCharacters()
    {
        var code = RandomCoder.GetSpecialChars(64);

        Assert.All(code, c => Assert.Contains(c, DefaultConsts.SpecialCharactersWithoutQuotes));
        Assert.DoesNotContain('\'', code);
        Assert.DoesNotContain('"', code);
    }

    /// <summary>
    /// 强密码默认覆盖四类字符
    /// </summary>
    [Fact]
    public void GetStrongPassword_CoversAllFourCharacterSets()
    {
        for (var i = 0; i < 20; i++)
        {
            var password = RandomCoder.GetStrongPassword();

            Assert.Equal(12, password.Length);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, c => DefaultConsts.SpecialCharactersWithoutQuotes.Contains(c));
        }
    }

    /// <summary>
    /// 强密码长度不足以覆盖四类字符时退化为合并源均匀采样
    /// </summary>
    [Fact]
    public void GetStrongPassword_WhenShorterThanSetCount_StillHonorsLength()
    {
        Assert.Equal(3, RandomCoder.GetStrongPassword(3).Length);
    }

    /// <summary>
    /// 指定自定义字符源时退化为从该源均匀采样
    /// </summary>
    [Fact]
    public void GetStrongPassword_WithCustomSource_OnlyUsesThatSource()
    {
        var password = RandomCoder.GetStrongPassword(16, "AB");

        Assert.Equal(16, password.Length);
        Assert.All(password, c => Assert.Contains(c, "AB"));
    }

    /// <summary>
    /// 按开关裁剪字符集
    /// </summary>
    [Fact]
    public void GetCustom_RespectsIncludeSwitches()
    {
        var digitsOnly = RandomCoder.GetCustom(32, includeNumbers: true, includeUpperLetters: false,
            includeLowerLetters: false, includeSpecialChars: false);
        Assert.All(digitsOnly, c => Assert.Contains(c, DefaultConsts.Digits));

        var lettersOnly = RandomCoder.GetCustom(32, includeNumbers: false, includeUpperLetters: true,
            includeLowerLetters: true, includeSpecialChars: false);
        Assert.All(lettersOnly, c => Assert.True(char.IsAsciiLetter(c)));
    }

    /// <summary>
    /// 一个开关都不开时回落到数字加大小写字母
    /// </summary>
    [Fact]
    public void GetCustom_WithAllSwitchesOff_FallsBackToAlphanumeric()
    {
        var code = RandomCoder.GetCustom(32, includeNumbers: false, includeUpperLetters: false,
            includeLowerLetters: false, includeSpecialChars: false);

        Assert.Equal(32, code.Length);
        Assert.All(code, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
    }

    /// <summary>
    /// 长度恰好等于所选字符类数量时，每类正好各出现一次
    /// </summary>
    [Fact]
    public void GetCustom_WhenLengthEqualsSetCount_CoversEverySetExactlyOnce()
    {
        for (var i = 0; i < 20; i++)
        {
            var code = RandomCoder.GetCustom(4, includeNumbers: true, includeUpperLetters: true,
                includeLowerLetters: true, includeSpecialChars: true);

            Assert.Equal(4, code.Length);
            Assert.Equal(1, code.Count(char.IsDigit));
            Assert.Equal(1, code.Count(char.IsUpper));
            Assert.Equal(1, code.Count(char.IsLower));
            Assert.Equal(1, code.Count(DefaultConsts.SpecialCharactersWithoutQuotes.Contains));
        }
    }

    /// <summary>
    /// 长度为零或负数时拒绝生成
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetNumber_WithNonPositiveLength_ThrowsArgumentOutOfRange(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = RandomCoder.GetNumber(length); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = RandomCoder.GetLetter(length); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = RandomCoder.GetSpecialChars(length); });
    }

    /// <summary>
    /// 自定义字符源为空串时拒绝生成
    /// </summary>
    [Fact]
    public void GetNumber_WithEmptySource_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => { _ = RandomCoder.GetNumber(6, string.Empty); });
    }

    /// <summary>
    /// 多次取值不应全部相同
    /// </summary>
    /// <remarks>
    /// 无法用单测证明随机性，但「50 次取 10 位数字全部撞车」意味着随机源坏掉了，属于必须报警的下限。
    /// </remarks>
    [Fact]
    public void GetNumber_AcrossManyCalls_ProducesVaryingValues()
    {
        var values = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            values.Add(RandomCoder.GetNumber(10));
        }

        Assert.True(values.Count > 45, $"50 次取值只得到 {values.Count} 个不同结果，随机源可能异常。");
    }

    /// <summary>
    /// 随机汉字应当返回指定数量的汉字
    /// </summary>
    /// <remarks>
    /// 【已知红灯 / 疑似缺陷】实现用 <c>Encoding.GetEncoding("GB2312")</c> 做区位码到汉字的转换，
    /// 但 .NET Core 起 GB2312 不在内置编码里，需要引用 <c>System.Text.Encoding.CodePages</c>
    /// 并调用 <c>Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)</c>；
    /// XiHan.Framework.Utils 既没有引用该包也没有注册提供程序，调用会直接抛异常。
    /// 按方法宣称的语义断言，缺陷已上报由主控裁决。
    /// </remarks>
    [Fact]
    public void GetChineseCharacters_ReturnsRequestedNumberOfChineseCharacters()
    {
        var text = RandomCoder.GetChineseCharacters(8);

        Assert.Equal(8, text.Length);
        Assert.All(text, c => Assert.InRange(c, '一', '龥'));
    }
}
