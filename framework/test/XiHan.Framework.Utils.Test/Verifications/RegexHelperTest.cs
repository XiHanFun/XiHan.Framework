using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.Utils.Test.Verifications;

public class RegexHelperTest
{
    [Fact]
    public void GuidRegex_ValidatesCorrectGuid()
    {
        // Arrange
        var validGuid = "a1b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6";
        var invalidGuid = "a1b2c3d4-e5f6-a7b8-c9d0";
        var regex = RegexHelper.GuidRegex();

        // Act & Assert
        Assert.Matches(regex, validGuid);
        Assert.DoesNotMatch(regex, invalidGuid);
    }

    [Fact]
    public void EmailRegex_ValidatesCorrectEmail()
    {
        // Arrange
        var validEmail = "test@example.com";
        var invalidEmail = "test@example";
        var regex = RegexHelper.EmailRegex();

        // Act & Assert
        Assert.Matches(regex, validEmail);
        Assert.DoesNotMatch(regex, invalidEmail);
    }

    [Fact]
    public void NumberTelRegex_ValidatesCorrectPhoneNumber()
    {
        // Arrange
        var validPhoneNumber = "02112345678";
        var invalidPhoneNumber = "021123";
        var regex = RegexHelper.NumberTelRegex();

        // Act & Assert
        Assert.Matches(regex, validPhoneNumber);
        Assert.DoesNotMatch(regex, invalidPhoneNumber);
    }

    [Fact]
    public void IntRegex_ValidatesCorrectInteger()
    {
        // Arrange
        var validPositiveInt = "123";
        var validNegativeInt = "-123";
        var invalidInt = "123.45";
        var regex = RegexHelper.IntRegex();

        // Act & Assert
        Assert.Matches(regex, validPositiveInt);
        Assert.Matches(regex, validNegativeInt);
        Assert.DoesNotMatch(regex, invalidInt);
    }

    [Fact]
    public void NumberRegex_ValidatesCorrectNumber()
    {
        // Arrange
        var validNumber = "123";
        var invalidNumber = "123abc";
        var regex = RegexHelper.NumberRegex();

        // Act & Assert
        Assert.Matches(regex, validNumber);
        Assert.DoesNotMatch(regex, invalidNumber);
    }

    [Fact]
    public void LetterRegex_ValidatesCorrectLetter()
    {
        // Arrange
        var validLetters = "abcDEF";
        var invalidLetters = "abc123";
        var regex = RegexHelper.LetterRegex();

        // Act & Assert
        Assert.Matches(regex, validLetters);
        Assert.DoesNotMatch(regex, invalidLetters);
    }

    [Fact]
    public void LetterCapitalRegex_ValidatesCorrectCapitalLetter()
    {
        // Arrange
        var validCapitalLetters = "ABCDEF";
        var invalidCapitalLetters = "ABCdef";
        var regex = RegexHelper.LetterCapitalRegex();

        // Act & Assert
        Assert.Matches(regex, validCapitalLetters);
        Assert.DoesNotMatch(regex, invalidCapitalLetters);
    }

    [Fact]
    public void LetterLowerRegex_ValidatesCorrectLowerLetter()
    {
        // Arrange
        var validLowerLetters = "abcdef";
        var invalidLowerLetters = "abcDEF";
        var regex = RegexHelper.LetterLowerRegex();

        // Act & Assert
        Assert.Matches(regex, validLowerLetters);
        Assert.DoesNotMatch(regex, invalidLowerLetters);
    }

    [Fact]
    public void NumberOrLetterRegex_ValidatesCorrectNumberOrLetter()
    {
        // Arrange
        var validNumberOrLetter = "abc123DEF";
        var invalidNumberOrLetter = "abc123@DEF";
        var regex = RegexHelper.NumberOrLetterRegex();

        // Act & Assert
        Assert.Matches(regex, validNumberOrLetter);
        Assert.DoesNotMatch(regex, invalidNumberOrLetter);
    }

    [Fact]
    public void ChineseRegex_ValidatesCorrectChinese()
    {
        // Arrange
        var validChinese = "中";
        var invalidChinese = "A";
        var regex = RegexHelper.ChineseRegex();

        // Act & Assert
        Assert.Matches(regex, validChinese);
        Assert.DoesNotMatch(regex, invalidChinese);
    }

    [Fact]
    public void ContainChineseRegex_ValidatesCorrectContainChinese()
    {
        // Arrange
        var validContainChinese = "中文";
        var invalidContainChinese = "ABC";
        var regex = RegexHelper.ContainChineseRegex();

        // Act & Assert
        Assert.Matches(regex, validContainChinese);
        Assert.DoesNotMatch(regex, invalidContainChinese);
    }

    [Fact]
    public void UrlRegex_ValidatesCorrectUrl()
    {
        // Arrange
        var validUrl = "https://www.example.com";
        var invalidUrl = "example";
        var regex = RegexHelper.UrlRegex();

        // Act & Assert
        Assert.Matches(regex, validUrl);
        Assert.DoesNotMatch(regex, invalidUrl);
    }

    [Fact]
    public void RequestSecurityParamsRegex_ValidatesCorrectSecurityParams()
    {
        // Arrange
        var validSecurityParams = "password=123456&username=test";
        var invalidSecurityParams = "username=test";
        var regex = RegexHelper.RequestSecurityParamsRegex();

        // Act & Assert
        Assert.Matches(regex, validSecurityParams);
        Assert.DoesNotMatch(regex, invalidSecurityParams);
    }

    [Fact]
    public void IpRegex_ValidatesCorrectIp()
    {
        // Arrange
        var validIp = "192.168.1.1";
        var invalidIp = "192.168.1.256";
        var regex = RegexHelper.IpRegex();

        // Act & Assert
        Assert.Matches(regex, validIp);
        Assert.DoesNotMatch(regex, invalidIp);
    }

    [Fact]
    public void WindowsPathRegex_ValidatesCorrectWindowsPath()
    {
        // Arrange
        var validWindowsPath = @"C:\folder\file.txt";
        var invalidWindowsPath = @"C:|folder|file.txt";
        var regex = RegexHelper.WindowsPathRegex();

        // Act & Assert
        Assert.Matches(regex, validWindowsPath);
        Assert.DoesNotMatch(regex, invalidWindowsPath);
    }

    [Fact]
    public void LinuxPathRegex_ValidatesCorrectLinuxPath()
    {
        // Arrange
        var validLinuxPath = "/home/user/file.txt";
        var invalidLinuxPath = "home|user|file.txt";
        var regex = RegexHelper.LinuxPathRegex();

        // Act & Assert
        Assert.Matches(regex, validLinuxPath);
        Assert.DoesNotMatch(regex, invalidLinuxPath);
    }

    [Fact]
    public void VirtualPathRegex_ValidatesCorrectVirtualPath()
    {
        // Arrange
        var validVirtualPath = "~/folder/file.txt";
        var invalidVirtualPath = "~folder/file.txt";
        var regex = RegexHelper.VirtualPathRegex();

        // Act & Assert
        Assert.Matches(regex, validVirtualPath);
        Assert.DoesNotMatch(regex, invalidVirtualPath);
    }

    [Fact]
    public void EmbeddedPathRegex_ValidatesCorrectEmbeddedPath()
    {
        // Arrange
        var validEmbeddedPath = "embedded://assembly/path/to/file.txt";
        var invalidEmbeddedPath = "assembly/path/to/file.txt";
        var regex = RegexHelper.EmbeddedPathRegex();

        // Act & Assert
        Assert.Matches(regex, validEmbeddedPath);
        Assert.DoesNotMatch(regex, invalidEmbeddedPath);
    }

    [Fact]
    public void MemoryPathRegex_ValidatesCorrectMemoryPath()
    {
        // Arrange
        var validMemoryPath = "memory://path/to/file.txt";
        var invalidMemoryPath = "path/to/file.txt";
        var regex = RegexHelper.MemoryPathRegex();

        // Act & Assert
        Assert.Matches(regex, validMemoryPath);
        Assert.DoesNotMatch(regex, invalidMemoryPath);
    }
}
