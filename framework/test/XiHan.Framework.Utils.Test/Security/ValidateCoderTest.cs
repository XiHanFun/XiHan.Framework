using System.Text.RegularExpressions;
using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Test.Security;

public class ValidateCoderTest
{
    [Fact]
    public void DefaultCharacterSources_ShouldBeDefinedCorrectly()
    {
        // 测试基础字符源是否正确定义
        Assert.Equal("0123456789", ValidateCoder.DefaultNumberSource);
        Assert.Equal("!@#$%^&*()-_=+[]{}|;:,.<>?/", ValidateCoder.DefaultSpecialCharSource);
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZ", ValidateCoder.DefaultUpperLetterSource);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz", ValidateCoder.DefaultLowerLetterSource);
    }

    [Fact]
    public void GetNumber_ReturnsNumericString()
    {
        // Act
        var result = ValidateCoder.GetNumber();

        // Assert
        Assert.Equal(6, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultNumberSource)}]+$", result);
    }

    [Fact]
    public void GetNumber_WithCustomLength_ReturnsCorrectLength()
    {
        // Arrange
        var length = 10;

        // Act
        var result = ValidateCoder.GetNumber(length);

        // Assert
        Assert.Equal(length, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultNumberSource)}]+$", result);
    }

    [Fact]
    public void GetNumber_WithCustomSource_UsesCustomSource()
    {
        // Arrange
        var source = "13579";

        // Act
        var result = ValidateCoder.GetNumber(6, source);

        // Assert
        Assert.Equal(6, result.Length);
        Assert.Matches("^[13579]+$", result);
    }

    [Fact]
    public void GetSpecialChars_ReturnsSpecialCharString()
    {
        // Act
        var result = ValidateCoder.GetSpecialChars();

        // Assert
        Assert.Equal(6, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultSpecialCharSource)}]+$", result);
    }

    [Fact]
    public void GetSpecialChars_WithCustomLength_ReturnsCorrectLength()
    {
        // Arrange
        var length = 5;

        // Act
        var result = ValidateCoder.GetSpecialChars(length);

        // Assert
        Assert.Equal(length, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultSpecialCharSource)}]+$", result);
    }

    [Fact]
    public void GetUpperLetter_ReturnsUppercaseString()
    {
        // Act
        var result = ValidateCoder.GetUpperLetter();

        // Assert
        Assert.Equal(6, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultUpperLetterSource)}]+$", result);
    }

    [Fact]
    public void GetUpperLetter_WithCustomLength_ReturnsCorrectLength()
    {
        // Arrange
        var length = 8;

        // Act
        var result = ValidateCoder.GetUpperLetter(length);

        // Assert
        Assert.Equal(length, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultUpperLetterSource)}]+$", result);
    }

    [Fact]
    public void GetLowerLetter_ReturnsLowercaseString()
    {
        // Act
        var result = ValidateCoder.GetLowerLetter();

        // Assert
        Assert.Equal(6, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultLowerLetterSource)}]+$", result);
    }

    [Fact]
    public void GetLowerLetter_WithCustomLength_ReturnsCorrectLength()
    {
        // Arrange
        var length = 9;

        // Act
        var result = ValidateCoder.GetLowerLetter(length);

        // Assert
        Assert.Equal(length, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultLowerLetterSource)}]+$", result);
    }

    [Fact]
    public void GetNumberOrLetter_ReturnsAlphanumericString()
    {
        // Act
        var result = ValidateCoder.GetNumberOrLetter();

        // Assert
        Assert.Equal(6, result.Length);

        // 验证结果中的字符都在基础字符源的组合范围内
        var expectedChars = ValidateCoder.DefaultNumberSource +
                            ValidateCoder.DefaultUpperLetterSource +
                            ValidateCoder.DefaultLowerLetterSource;
        Assert.True(result.All(c => expectedChars.Contains(c)));
    }

    [Fact]
    public void GetNumberOrLetter_WithCustomLength_ReturnsCorrectLength()
    {
        // Arrange
        var length = 12;

        // Act
        var result = ValidateCoder.GetNumberOrLetter(length);

        // Assert
        Assert.Equal(length, result.Length);

        // 验证结果中的字符都在基础字符源的组合范围内
        var expectedChars = ValidateCoder.DefaultNumberSource +
                            ValidateCoder.DefaultUpperLetterSource +
                            ValidateCoder.DefaultLowerLetterSource;
        Assert.True(result.All(c => expectedChars.Contains(c)));
    }

    [Fact]
    public void GetStrongPassword_ReturnsStrongPasswordString()
    {
        // Act
        var result = ValidateCoder.GetStrongPassword();

        // Assert
        Assert.Equal(12, result.Length); // 默认长度为12

        // 验证结果中的字符都在基础字符源的组合范围内
        var expectedChars = ValidateCoder.DefaultNumberSource +
                            ValidateCoder.DefaultUpperLetterSource +
                            ValidateCoder.DefaultLowerLetterSource +
                            ValidateCoder.DefaultSpecialCharSource;
        Assert.True(result.All(c => expectedChars.Contains(c)));
    }

    [Fact]
    public void GetStrongPassword_WithCustomLength_ReturnsCorrectLength()
    {
        // Arrange
        var length = 16;

        // Act
        var result = ValidateCoder.GetStrongPassword(length);

        // Assert
        Assert.Equal(length, result.Length);

        // 验证结果中的字符都在基础字符源的组合范围内
        var expectedChars = ValidateCoder.DefaultNumberSource +
                            ValidateCoder.DefaultUpperLetterSource +
                            ValidateCoder.DefaultLowerLetterSource +
                            ValidateCoder.DefaultSpecialCharSource;
        Assert.True(result.All(c => expectedChars.Contains(c)));
    }

    [Fact]
    public void GetCustom_WithAllOptions_ReturnsStringWithAllCharTypes()
    {
        // Act
        var result = ValidateCoder.GetCustom(12, true, true, true, true);

        // Assert
        Assert.Equal(12, result.Length);

        // 验证结果中的字符都在基础字符源的组合范围内
        var expectedChars = ValidateCoder.DefaultNumberSource +
                            ValidateCoder.DefaultUpperLetterSource +
                            ValidateCoder.DefaultLowerLetterSource +
                            ValidateCoder.DefaultSpecialCharSource;
        Assert.True(result.All(c => expectedChars.Contains(c)));
    }

    [Fact]
    public void GetCustom_OnlyNumbers_ReturnsOnlyNumbers()
    {
        // Act
        var result = ValidateCoder.GetCustom(8, true, false, false);

        // Assert
        Assert.Equal(8, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultNumberSource)}]+$", result);
    }

    [Fact]
    public void GetCustom_OnlyUpperLetters_ReturnsOnlyUpperLetters()
    {
        // Act
        var result = ValidateCoder.GetCustom(7, false, true, false);

        // Assert
        Assert.Equal(7, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultUpperLetterSource)}]+$", result);
    }

    [Fact]
    public void GetCustom_OnlyLowerLetters_ReturnsOnlyLowerLetters()
    {
        // Act
        var result = ValidateCoder.GetCustom(9, false, false);

        // Assert
        Assert.Equal(9, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultLowerLetterSource)}]+$", result);
    }

    [Fact]
    public void GetCustom_OnlySpecialChars_ReturnsOnlySpecialChars()
    {
        // Act
        var result = ValidateCoder.GetCustom(10, false, false, false, true);

        // Assert
        Assert.Equal(10, result.Length);
        Assert.Matches($"^[{Regex.Escape(ValidateCoder.DefaultSpecialCharSource)}]+$", result);
    }

    [Fact]
    public void GetCustom_NoOptions_ReturnsAlphanumericString()
    {
        // Act
        var result = ValidateCoder.GetCustom(8, false, false, false);

        // Assert
        Assert.Equal(8, result.Length);

        // 验证结果中的字符都在基础字符源的组合范围内
        var expectedChars = ValidateCoder.DefaultNumberSource +
                            ValidateCoder.DefaultUpperLetterSource +
                            ValidateCoder.DefaultLowerLetterSource;
        Assert.True(result.All(c => expectedChars.Contains(c)));
    }

    [Fact]
    public void GetChineseCharacters_ReturnsChineseCharacters()
    {
        // Act
        var result = ValidateCoder.GetChineseCharacters();

        // Assert
        Assert.Equal(6, result.Length);
        // 验证是否是汉字
        var regex = new Regex(@"^[\u4e00-\u9fa5]+$");
        Assert.Matches(regex, result);
    }

    [Fact]
    public void GetChineseCharacters_WithCustomLength_ReturnsCorrectLength()
    {
        // Arrange
        var length = 4;

        // Act
        var result = ValidateCoder.GetChineseCharacters(length);

        // Assert
        Assert.Equal(length, result.Length);
        // 验证是否是汉字
        var regex = new Regex(@"^[\u4e00-\u9fa5]+$");
        Assert.Matches(regex, result);
    }
}
