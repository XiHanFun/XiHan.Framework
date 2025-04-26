using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.Utils.Test.Verifications;

public class DataCheckerTest
{
    [Fact]
    public void IsMatch_WithMatchingPattern_ReturnsTrue()
    {
        // Arrange
        var input = "abc123";
        var pattern = @"^[a-z]+\d+$";

        // Act
        var result = DataChecker.IsMatch(input, pattern);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsMatch_WithNonMatchingPattern_ReturnsFalse()
    {
        // Arrange
        var input = "123abc";
        var pattern = @"^[a-z]+\d+$";

        // Act
        var result = DataChecker.IsMatch(input, pattern);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsGuid_WithValidGuid_ReturnsTrue()
    {
        // Arrange
        var validGuid = "a480500f-a181-4d3d-8ada-461f69eecfdd";

        // Act
        var result = DataChecker.IsGuid(validGuid);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsGuid_WithInvalidGuid_ReturnsFalse()
    {
        // Arrange
        var invalidGuid = "a480500f-a181-4d3d-8ada";

        // Act
        var result = DataChecker.IsGuid(invalidGuid);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNumberTel_WithValidPhoneNumber_ReturnsTrue()
    {
        // Arrange
        var validPhone = "02112345678";

        // Act
        var result = DataChecker.IsNumberTel(validPhone);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNumberTel_WithInvalidPhoneNumber_ReturnsFalse()
    {
        // Arrange
        var invalidPhone = "021123";

        // Act
        var result = DataChecker.IsNumberTel(invalidPhone);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNumberPeople_WithValid18DigitIDCard_ReturnsTrue()
    {
        // Arrange
        // 注意：这是一个虚构的、符合规则的身份证号码
        var valid18DigitIdCard = "110101199001011234";

        // Act
        var result = DataChecker.IsNumberPeople(valid18DigitIdCard);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNumberPeople_WithInvalid18DigitIDCard_ReturnsFalse()
    {
        // Arrange
        var invalid18DigitIdCard = "110101199099011234"; // 不符合日期规则

        // Act
        var result = DataChecker.IsNumberPeople(invalid18DigitIdCard);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEmail_WithValidEmail_ReturnsTrue()
    {
        // Arrange
        var validEmail = "test@example.com";

        // Act
        var result = DataChecker.IsEmail(validEmail);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsEmail_WithInvalidEmail_ReturnsFalse()
    {
        // Arrange
        var invalidEmail = "test@example";

        // Act
        var result = DataChecker.IsEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNumber_WithValidNumber_ReturnsTrue()
    {
        // Arrange
        var validNumber = "123456";

        // Act
        var result = DataChecker.IsNumber(validNumber);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNumber_WithInvalidNumber_ReturnsFalse()
    {
        // Arrange
        var invalidNumber = "123abc";

        // Act
        var result = DataChecker.IsNumber(invalidNumber);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsInt_WithValidInt_ReturnsTrue()
    {
        // Arrange
        var validInt = "12345";

        // Act
        var result = DataChecker.IsInt(validInt);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInt_WithInvalidInt_ReturnsFalse()
    {
        // Arrange
        var invalidInt = "12345.67";

        // Act
        var result = DataChecker.IsInt(invalidInt);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLetter_WithOnlyLetters_ReturnsTrue()
    {
        // Arrange
        var validLetter = "abcDEF";

        // Act
        var result = DataChecker.IsLetter(validLetter);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLetter_WithNonLetters_ReturnsFalse()
    {
        // Arrange
        var invalidLetter = "abc123";

        // Act
        var result = DataChecker.IsLetter(invalidLetter);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLetterCapital_WithAllCapitalLetters_ReturnsTrue()
    {
        // Arrange
        var validCapital = "ABCDEF";

        // Act
        var result = DataChecker.IsLetterCapital(validCapital);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLetterCapital_WithMixedCaseLetters_ReturnsFalse()
    {
        // Arrange
        var invalidCapital = "ABCdef";

        // Act
        var result = DataChecker.IsLetterCapital(invalidCapital);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLetterLower_WithAllLowercaseLetters_ReturnsTrue()
    {
        // Arrange
        var validLower = "abcdef";

        // Act
        var result = DataChecker.IsLetterLower(validLower);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLetterLower_WithMixedCaseLetters_ReturnsFalse()
    {
        // Arrange
        var invalidLower = "abcDEF";

        // Act
        var result = DataChecker.IsLetterLower(invalidLower);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNumberOrLetter_WithValidInput_ReturnsTrue()
    {
        // Arrange
        var validInput = "abc123DEF";

        // Act
        var result = DataChecker.IsNumberOrLetter(validInput);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNumberOrLetter_WithInvalidInput_ReturnsFalse()
    {
        // Arrange
        var invalidInput = "abc@123";

        // Act
        var result = DataChecker.IsNumberOrLetter(invalidInput);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsContainChinese_WithChineseCharacters_ReturnsTrue()
    {
        // Arrange
        var validChinese = "中文";

        // Act
        var result = DataChecker.IsContainChinese(validChinese);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsContainChinese_WithoutChineseCharacters_ReturnsFalse()
    {
        // Arrange
        var invalidChinese = "abc123";

        // Act
        var result = DataChecker.IsContainChinese(invalidChinese);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsChinese_WithChineseCharacters_ReturnsTrue()
    {
        // Arrange
        var validChinese = "中";

        // Act
        var result = DataChecker.IsChinese(validChinese);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsChinese_WithNonChineseCharacters_ReturnsFalse()
    {
        // Arrange
        var invalidChinese = "A";

        // Act
        var result = DataChecker.IsChinese(invalidChinese);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUrl_WithValidUrl_ReturnsTrue()
    {
        // Arrange
        var validUrl = "https://www.example.com";

        // Act
        var result = DataChecker.IsUrl(validUrl);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUrl_WithInvalidUrl_ReturnsFalse()
    {
        // Arrange
        var invalidUrl = "example";

        // Act
        var result = DataChecker.IsUrl(invalidUrl);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsIp_WithValidIp_ReturnsTrue()
    {
        // Arrange
        var validIp = "192.168.1.1";

        // Act
        var result = DataChecker.IsIp(validIp);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsIp_WithInvalidIp_ReturnsFalse()
    {
        // Arrange
        var invalidIp = "192.168.1.256"; // 256 超出了 IP 地址的有效范围

        // Act
        var result = DataChecker.IsIp(invalidIp);

        // Assert
        Assert.False(result);
    }
}
