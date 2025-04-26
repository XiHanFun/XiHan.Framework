using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Test.Security;

public class MaskHelperTests
{
    [Fact]
    public void Mask_ReturnsMaskedString_WhenInputIsValid()
    {
        var result = "1234567890".Mask(3, 2);
        Assert.Equal("123*****90", result);
    }

    [Fact]
    public void Mask_ReturnsEmptyString_WhenInputIsNullOrWhitespace()
    {
        Assert.Equal(string.Empty, "".Mask(3, 2));
        Assert.Equal(string.Empty, "   ".Mask(3, 2));
    }

    [Fact]
    public void MaskPhone_ReturnsMaskedPhone_WhenInputIsValid()
    {
        var result = MaskHelper.MaskPhone("13812345678");
        Assert.Equal("138****5678", result);
    }

    [Fact]
    public void MaskPhone_ReturnsMaskedString_WhenInputIsTooShort()
    {
        var result = MaskHelper.MaskPhone("12345");
        Assert.Equal("*****", result);
    }

    [Fact]
    public void MaskIdCard_ReturnsMaskedIdCard_WhenInputIsValid()
    {
        var result = MaskHelper.MaskIdCard("11010119800101001X");
        Assert.Equal("1101***********1X", result);
    }

    [Fact]
    public void MaskIdCard_ReturnsMaskedString_WhenInputIsTooShort()
    {
        var result = MaskHelper.MaskIdCard("1234567");
        Assert.Equal("*******", result);
    }

    [Fact]
    public void MaskEmail_ReturnsMaskedEmail_WhenInputIsValid()
    {
        var result = MaskHelper.MaskEmail("test@example.com");
        Assert.Equal("tes*@exa****.com", result);
    }

    [Fact]
    public void MaskEmail_ReturnsOriginalEmail_WhenInputIsInvalid()
    {
        var result = MaskHelper.MaskEmail("invalid-email");
        Assert.Equal("invalid-email", result);
    }

    [Fact]
    public void MaskChineseName_ReturnsMaskedName_WhenInputIsValid()
    {
        var result = MaskHelper.MaskChineseName("张三");
        Assert.Equal("张*", result);

        result = MaskHelper.MaskChineseName("欧阳娜娜");
        Assert.Equal("欧**娜", result);
    }

    [Fact]
    public void MaskChineseName_ReturnsOriginalName_WhenInputIsSingleCharacter()
    {
        var result = MaskHelper.MaskChineseName("张");
        Assert.Equal("张", result);
    }

    [Fact]
    public void MaskAddress_ReturnsMaskedAddress_WhenInputIsValid()
    {
        var result = MaskHelper.MaskAddress("北京市朝阳区建国路1号");
        Assert.Equal("北京市朝阳区****号", result);
    }

    [Fact]
    public void MaskAddress_ReturnsOriginalAddress_WhenInputIsTooShort()
    {
        var result = MaskHelper.MaskAddress("北京市");
        Assert.Equal("北京市", result);
    }

    [Fact]
    public void MaskPassword_ReturnsMaskedPassword_WhenInputIsValid()
    {
        var result = MaskHelper.MaskPassword("mypassword");
        Assert.Equal("**********", result);
    }

    [Fact]
    public void MaskPassword_ReturnsEmptyString_WhenInputIsNullOrEmpty()
    {
        Assert.Equal(string.Empty, MaskHelper.MaskPassword(""));
    }

    [Fact]
    public void MaskLicensePlate_ReturnsMaskedPlate_WhenInputIsValid()
    {
        var result = MaskHelper.MaskLicensePlate("京A12345");
        Assert.Equal("京A****5", result);
    }

    [Fact]
    public void MaskLicensePlate_ReturnsOriginalPlate_WhenInputIsTooShort()
    {
        var result = MaskHelper.MaskLicensePlate("京A");
        Assert.Equal("京A", result);
    }

    [Fact]
    public void MaskUrlParams_ReturnsMaskedUrl_WhenInputContainsSensitiveParams()
    {
        var result = MaskHelper.MaskUrlParams("https://example.com?token=12345");
        Assert.Equal("https://example.com?******", result);
    }

    [Fact]
    public void MaskUrlParams_ReturnsOriginalUrl_WhenInputHasNoSensitiveParams()
    {
        var result = MaskHelper.MaskUrlParams("https://example.com");
        Assert.Equal("https://example.com", result);
    }

    [Fact]
    public void MaskJson_ReturnsMaskedJson_WhenInputContainsSensitiveFields()
    {
        var json = @"{
            ""name"": ""张三"",
            ""phone"": ""13812345678"",
            ""idCard"": ""11010119800101001X"",
            ""bankCard"": ""6222020200112233445"",
            ""email"": ""test@example.com"",
            ""password"": ""mypassword"",
            ""address"": ""北京市朝阳区建国路1号"",
            ""licensePlate"": ""京A12345"",
            ""url"": ""https://example.com?token=12345"",
            ""otp"": ""123456"",
            ""authorization"": ""Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"",
            ""token"": ""abcdef123456""
        }";

        var result = MaskHelper.MaskJson(json);

        Assert.Contains("\"name\": \"张*\"", result);
        Assert.Contains("\"phone\": \"138****5678\"", result);
        Assert.Contains("\"idCard\": \"1101***********1X\"", result);
        Assert.Contains("\"bankCard\": \"6222********3445\"", result);
        Assert.Contains("\"email\": \"tes*@exa****.com\"", result);
        Assert.Contains("\"password\": \"**********\"", result);
        Assert.Contains("\"address\": \"北京市朝阳区****号\"", result);
        Assert.Contains("\"licensePlate\": \"京A****5\"", result);
        Assert.Contains("\"url\": \"https://example.com?******\"", result);
        Assert.Contains("\"otp\": \"******\"", result);
        Assert.Contains("\"authorization\": \"******\"", result);
        Assert.Contains("\"token\": \"******\"", result);
    }

    [Fact]
    public void MaskJson_HandlesEmptyOrNullInput()
    {
        Assert.Equal(string.Empty, MaskHelper.MaskJson(string.Empty));
    }

    [Fact]
    public void MaskJson_HandlesCaseInsensitiveFields()
    {
        var json = @"{
            ""NAME"": ""张三"",
            ""Phone"": ""13812345678"",
            ""IdCard"": ""11010119800101001X"",
            ""BANKcard"": ""6222020200112233445"",
            ""EMAIL"": ""test@example.com"",
            ""PassWord"": ""mypassword"",
            ""Address"": ""北京市朝阳区建国路1号"",
            ""LICENSEplate"": ""京A12345"",
            ""URL"": ""https://example.com?token=12345"",
            ""OTP"": ""123456"",
            ""Authorization"": ""Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"",
            ""TOKEN"": ""abcdef123456""
        }";

        var result = MaskHelper.MaskJson(json);

        Assert.Contains("\"name\": \"张*\"", result);
        Assert.Contains("\"phone\": \"138****5678\"", result);
        Assert.Contains("\"idCard\": \"1101***********1X\"", result);
        Assert.Contains("\"bankCard\": \"6222********3445\"", result);
        Assert.Contains("\"email\": \"tes*@exa****.com\"", result);
        Assert.Contains("\"password\": \"**********\"", result);
        Assert.Contains("\"address\": \"北京市朝阳区****号\"", result);
        Assert.Contains("\"licensePlate\": \"京A****5\"", result);
        Assert.Contains("\"url\": \"https://example.com?******\"", result);
        Assert.Contains("\"otp\": \"******\"", result);
        Assert.Contains("\"authorization\": \"******\"", result);
        Assert.Contains("\"token\": \"******\"", result);
    }

    [Fact]
    public void MaskJson_HandlesNoSensitiveFields()
    {
        var json = @"{""foo"": ""bar"", ""baz"": 123}";
        var result = MaskHelper.MaskJson(json);
        Assert.Equal(json, result);
    }

    [Fact]
    public void MaskJson_HandlesMixedCaseForOtpAuthorizationToken()
    {
        var json = @"{
            ""oTp"": ""123456"",
            ""AuThOrIzAtIoN"": ""Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"",
            ""toKEN"": ""abcdef123456""
        }";

        var result = MaskHelper.MaskJson(json);

        Assert.Contains("\"otp\": \"******\"", result);
        Assert.Contains("\"authorization\": \"******\"", result);
        Assert.Contains("\"token\": \"******\"", result);
    }

    [Fact]
    public void GenerateCaseInsensitivePattern_CreatesCorrectPattern()
    {
        // 由于方法是私有的，我们可以通过测试结果的行为来间接验证
        var json = @"{""TEST"": ""value""}";
        var result = MaskHelper.MaskJson(json);

        // 如果大小写不敏感模式生成正确，即使使用不同的大小写，结果应该保持不变
        var json2 = @"{""test"": ""value""}";
        var result2 = MaskHelper.MaskJson(json2);

        var json3 = @"{""TeSt"": ""value""}";
        var result3 = MaskHelper.MaskJson(json3);

        // 这些JSON虽然字段名大小写不同，但应该有相同的处理结果
        Assert.Equal(result, result2);
        Assert.Equal(result2, result3);
    }
}
