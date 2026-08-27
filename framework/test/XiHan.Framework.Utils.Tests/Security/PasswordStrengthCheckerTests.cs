// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Constants;
using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 密码强度检测器测试
/// </summary>
/// <remarks>
/// 检测器是短路式的：任何一项不满足立刻返回，后面的加分项不再累计，
/// 所以「失败时的分值」本身也是契约的一部分（前端会拿它画强度条），必须逐项锁死。
/// 评分口径：长度 ≥ 12 加 20，大写/小写/数字/特殊字符各加 20，命中黑名单扣 30。
/// </remarks>
public class PasswordStrengthCheckerTests
{
    /// <summary>
    /// 空密码直接判否
    /// </summary>
    [Fact]
    public void CheckPasswordStrength_WithEmptyPassword_ReturnsZeroScore()
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength(string.Empty);

        Assert.False(result.IsStrong);
        Assert.Equal("密码不能为空", result.Message);
        Assert.Equal(0, result.Score);
    }

    /// <summary>
    /// 长度不足八位时短路，不累计任何加分
    /// </summary>
    [Fact]
    public void CheckPasswordStrength_WhenShorterThanEight_ShortCircuitsWithZeroScore()
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength("Ab1!");

        Assert.False(result.IsStrong);
        Assert.Equal("密码长度不足8位", result.Message);
        Assert.Equal(0, result.Score);
    }

    /// <summary>
    /// 缺少某类字符时在对应位置短路，分值反映已通过的检查项
    /// </summary>
    [Theory]
    [InlineData("abcdefg1!", "密码必须包含至少一个大写字母", 0)]
    [InlineData("ABCDEFG1!", "密码必须包含至少一个小写字母", 20)]
    [InlineData("Abcdefg!", "密码必须包含至少一个数字", 40)]
    [InlineData("Abcdefg1", "密码必须包含至少一个特殊字符", 60)]
    public void CheckPasswordStrength_WhenCharacterClassMissing_ShortCircuitsWithPartialScore(
        string password, string expectedMessage, int expectedScore)
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength(password);

        Assert.False(result.IsStrong);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(expectedScore, result.Score);
    }

    /// <summary>
    /// 满足全部四类字符的合格密码
    /// </summary>
    [Fact]
    public void CheckPasswordStrength_WithAllCharacterClasses_ReturnsStrong()
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength("Abcdefg1!");

        Assert.True(result.IsStrong);
        Assert.Equal("密码强度良好", result.Message);
        Assert.Equal(80, result.Score);
    }

    /// <summary>
    /// 长度达到 12 位额外加 20 分
    /// </summary>
    [Fact]
    public void CheckPasswordStrength_WhenAtLeastTwelveChars_AddsLengthBonus()
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength("Abcdefghij1!");

        Assert.True(result.IsStrong);
        Assert.Equal(100, result.Score);
    }

    /// <summary>
    /// 命中内置弱密码模式时判否并扣 30 分
    /// </summary>
    [Fact]
    public void CheckPasswordStrength_WhenContainsWeakPattern_RejectsWithPenalty()
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength("Abc123456!x");

        Assert.False(result.IsStrong);
        Assert.Equal("密码过于简单，包含弱密码模式", result.Message);
        Assert.Equal(50, result.Score);
    }

    /// <summary>
    /// 内置弱密码模式按子串命中
    /// </summary>
    [Theory]
    [InlineData("Xpassword1!")]
    [InlineData("Ax111111b1!")]
    [InlineData("Ax123123b1!")]
    public void CheckPasswordStrength_MatchesBuiltInBlacklistAsSubstring(string password)
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength(password);

        Assert.False(result.IsStrong);
        Assert.Equal("密码过于简单，包含弱密码模式", result.Message);
    }

    /// <summary>
    /// 命中自定义黑名单时判否并扣 30 分
    /// </summary>
    [Fact]
    public void CheckPasswordStrength_WhenContainsCustomBlacklistWord_RejectsWithPenalty()
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength("AbcXihan1!", ["Xihan"]);

        Assert.False(result.IsStrong);
        Assert.Equal("密码包含禁止使用的词汇", result.Message);
        Assert.Equal(50, result.Score);
    }

    /// <summary>
    /// 自定义黑名单为空集合时不影响判定
    /// </summary>
    [Fact]
    public void CheckPasswordStrength_WithEmptyCustomBlacklist_KeepsResultUnchanged()
    {
        var result = PasswordStrengthChecker.CheckPasswordStrength("Abcdefg1!", []);

        Assert.True(result.IsStrong);
        Assert.Equal(80, result.Score);
    }

    /// <summary>
    /// 结果对象的字符串表示包含三项信息
    /// </summary>
    [Fact]
    public void PasswordStrengthResult_ToString_ContainsAllFields()
    {
        var text = new PasswordStrengthResult(true, "密码强度良好", 80).ToString();

        Assert.Equal("Strong: True, Message: 密码强度良好, Score: 80", text);
    }

    /// <summary>
    /// 生成的密码长度不足八位时拒绝
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void GeneratePassword_WhenLengthBelowEight_ThrowsArgumentException(int length)
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = PasswordStrengthChecker.GeneratePassword(length); });

        Assert.Contains("8位", exception.Message);
    }

    /// <summary>
    /// 默认生成的密码含四类字符且能通过自身的强度检测
    /// </summary>
    [Fact]
    public void GeneratePassword_ByDefault_PassesOwnStrengthCheck()
    {
        for (var i = 0; i < 20; i++)
        {
            var password = PasswordStrengthChecker.GeneratePassword(16);

            Assert.Equal(16, password.Length);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, c => DefaultConsts.SpecialCharacters.Contains(c));
            Assert.True(PasswordStrengthChecker.CheckPasswordStrength(password).IsStrong);
        }
    }

    /// <summary>
    /// 关闭特殊字符后生成的密码只含字母与数字
    /// </summary>
    [Fact]
    public void GeneratePassword_WithoutSpecialChars_ProducesAlphanumericOnly()
    {
        for (var i = 0; i < 20; i++)
        {
            var password = PasswordStrengthChecker.GeneratePassword(12, includeSpecialChars: false);

            Assert.Equal(12, password.Length);
            Assert.All(password, c => Assert.True(char.IsLetterOrDigit(c)));
        }
    }

    /// <summary>
    /// 生成的密码每次都不同
    /// </summary>
    [Fact]
    public void GeneratePassword_ProducesDistinctValues()
    {
        var passwords = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            passwords.Add(PasswordStrengthChecker.GeneratePassword(16));
        }

        Assert.Equal(50, passwords.Count);
    }
}
