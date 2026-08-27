// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 日志敏感数据脱敏器测试
/// </summary>
/// <remarks>
/// 脱敏是安全边界，两个方向的错都要钉住：
/// <list type="bullet">
///   <item>漏掩（密码 / 令牌 / 密钥 / 身份证落进日志）＝ 凭证泄露；</item>
///   <item>过掩（把 <c>Token_Type</c>、<c>Last_Password_Change_Time</c>、<c>Max_Output_Tokens</c>
///         这类「关于秘密的元数据」也掩掉）＝ 审计线索一起丢失。</item>
/// </list>
/// 因此正反两组用例都用穷举式 <c>[Theory]</c> 覆盖，且锁死掩码占位符本身——它会进日志/落库，属对外契约。
/// </remarks>
public class LogSanitizerTests
{
    /// <summary>
    /// 掩码占位符是落库可见的对外契约，不允许静默变更
    /// </summary>
    [Fact]
    public void Mask_IsThreeAsterisks()
    {
        Assert.Equal("***", LogSanitizer.Mask);
    }

    /// <summary>
    /// 空名字不视为敏感
    /// </summary>
    /// <param name="name">待判定的名字</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSensitiveName_WhenNullOrWhiteSpace_ReturnsFalse(string? name)
    {
        Assert.False(LogSanitizer.IsSensitiveName(name));
    }

    /// <summary>
    /// 秘密本身的名字命中敏感判定（归一化后去分隔符、忽略大小写）
    /// </summary>
    /// <param name="name">待判定的名字</param>
    [Theory]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("Pwd")]
    [InlineData("passwd")]
    [InlineData("pass-word")]
    [InlineData("clientSecret")]
    [InlineData("refresh_token")]
    [InlineData("credential")]
    [InlineData("Authorization")]
    [InlineData("Set-Cookie")]
    [InlineData("X-Api-Key")]
    [InlineData("AccessKey")]
    [InlineData("privateKey")]
    [InlineData("Connection_String")]
    [InlineData("salt")]
    [InlineData("signature")]
    [InlineData("sessionId")]
    [InlineData("recoveryCode")]
    [InlineData("otpCode")]
    [InlineData("oneTimePassword")]
    [InlineData("twoFactor")]
    [InlineData("verifyCode")]
    [InlineData("verificationCode")]
    [InlineData("bankCard")]
    [InlineData("cardNo")]
    [InlineData("cardNumber")]
    [InlineData("accountNo")]
    [InlineData("idCard")]
    [InlineData("identityCard")]
    [InlineData("idNumber")]
    public void IsSensitiveName_WhenSecretName_ReturnsTrue(string name)
    {
        Assert.True(LogSanitizer.IsSensitiveName(name));
    }

    /// <summary>
    /// 命中敏感词但以元数据后缀结尾的名字不掩码（它们是审计最需要看的信息）
    /// </summary>
    /// <param name="name">待判定的名字</param>
    [Theory]
    [InlineData("Last_Password_Change_Time")]
    [InlineData("Password_Expiration_Time")]
    [InlineData("PasswordExpiration")]
    [InlineData("SecretDate")]
    [InlineData("Access_Token_Lifetime")]
    [InlineData("TokenExpires")]
    [InlineData("TokenExpiry")]
    [InlineData("Token_Type")]
    [InlineData("TokenKind")]
    [InlineData("Signature_Type")]
    [InlineData("Max_Output_Tokens")]
    [InlineData("TokenCount")]
    [InlineData("SessionTotal")]
    [InlineData("PasswordLength")]
    [InlineData("TokenSize")]
    [InlineData("CredentialStatus")]
    [InlineData("SessionState")]
    [InlineData("SessionEnabled")]
    [InlineData("SecretDisabled")]
    [InlineData("SignatureAlgorithm")]
    [InlineData("SecretVersion")]
    [InlineData("CookieMode")]
    [InlineData("TokenFormat")]
    [InlineData("SecretPolicy")]
    [InlineData("TokenStrategy")]
    [InlineData("PasswordAttempts")]
    public void IsSensitiveName_WhenMetadataAboutSecret_ReturnsFalse(string name)
    {
        Assert.False(LogSanitizer.IsSensitiveName(name));
    }

    /// <summary>
    /// 普通业务字段不误伤，特别是归一化后可能碰上关键字的名字
    /// </summary>
    /// <param name="name">待判定的名字</param>
    [Theory]
    [InlineData("userName")]
    [InlineData("email")]
    [InlineData("orderId")]
    [InlineData("traceId")]
    [InlineData("amount")]
    [InlineData("remark")]
    [InlineData("Not_Processed")]
    public void IsSensitiveName_WhenBusinessField_ReturnsFalse(string name)
    {
        Assert.False(LogSanitizer.IsSensitiveName(name));
    }

    /// <summary>
    /// 空内容原样返回，不产生 "***" 之类的噪声
    /// </summary>
    /// <param name="content">原始内容</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskSensitiveData_WhenNullOrWhiteSpace_ReturnsInput(string? content)
    {
        Assert.Equal(content, LogSanitizer.MaskSensitiveData(content));
    }

    /// <summary>
    /// JSON 中敏感键的字符串值整体掩码，非敏感键保持原样
    /// </summary>
    [Fact]
    public void MaskSensitiveData_WhenJsonSensitiveKey_MasksValueOnly()
    {
        var masked = LogSanitizer.MaskSensitiveData("{\"apiKey\":\"sk-live-123\",\"userName\":\"tom\"}");

        Assert.Equal("{\"apiKey\":\"***\",\"userName\":\"tom\"}", masked);
    }

    /// <summary>
    /// JSON 中敏感键的数字值同样被掩码（掩码结果是字符串字面量）
    /// </summary>
    [Fact]
    public void MaskSensitiveData_WhenJsonSensitiveKeyHasNumericValue_MasksValue()
    {
        var masked = LogSanitizer.MaskSensitiveData("{\"password\":12345}");

        Assert.Equal("{\"password\":\"***\"}", masked);
    }

    /// <summary>
    /// 元数据键的值必须原样保留，否则审计价值被一起掩掉
    /// </summary>
    [Fact]
    public void MaskSensitiveData_WhenJsonMetadataKey_KeepsValue()
    {
        const string Content = "{\"tokenType\":\"Bearer\",\"expiresIn\":3600}";

        Assert.Equal(Content, LogSanitizer.MaskSensitiveData(Content));
    }

    /// <summary>
    /// 表单风格键值对按键名逐个判定，掩码不破坏分隔结构
    /// </summary>
    [Fact]
    public void MaskSensitiveData_WhenFormPairs_MasksSensitiveKeysOnly()
    {
        var masked = LogSanitizer.MaskSensitiveData("password=secret&user=tom");

        Assert.Equal("password=***&user=tom", masked);
    }

    /// <summary>
    /// 18 位身份证号按首尾保留、中段掩码处理
    /// </summary>
    [Fact]
    public void MaskSensitiveData_When18DigitIdCard_MasksMiddle()
    {
        var masked = LogSanitizer.MaskSensitiveData("用户 11010119900307123X 已实名");

        Assert.Equal("用户 110***23X 已实名", masked);
    }

    /// <summary>
    /// 15 位旧身份证号同样掩码中段
    /// </summary>
    [Fact]
    public void MaskSensitiveData_When15DigitIdCard_MasksMiddle()
    {
        var masked = LogSanitizer.MaskSensitiveData("旧证 110101900307123 存档");

        Assert.Equal("旧证 110***123 存档", masked);
    }

    /// <summary>
    /// 长数字串不是身份证号，不得被误掩（前后有数字边界断言保护）
    /// </summary>
    [Fact]
    public void MaskSensitiveData_WhenLongDigitRun_LeavesUntouched()
    {
        const string Content = "12345678901234567890";

        Assert.Equal(Content, LogSanitizer.MaskSensitiveData(Content));
    }

    /// <summary>
    /// 空查询串原样返回
    /// </summary>
    /// <param name="queryString">原始查询串</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskQueryString_WhenNullOrWhiteSpace_ReturnsInput(string? queryString)
    {
        Assert.Equal(queryString, LogSanitizer.MaskQueryString(queryString));
    }

    /// <summary>
    /// 查询串只掩敏感键的值，问号与连接符结构保持不变
    /// </summary>
    [Fact]
    public void MaskQueryString_WhenSensitiveKey_MasksValueAndKeepsStructure()
    {
        var masked = LogSanitizer.MaskQueryString("?token=abc123&page=2");

        Assert.Equal("?token=***&page=2", masked);
    }

    /// <summary>
    /// 键名不敏感但值是身份证号时，仍按身份证模式掩码
    /// </summary>
    [Fact]
    public void MaskQueryString_WhenValueIsIdCard_MasksMiddle()
    {
        var masked = LogSanitizer.MaskQueryString("?idNo=11010119900307123X");

        Assert.Equal("?idNo=110***23X", masked);
    }

    /// <summary>
    /// 字段名敏感时无论值是什么类型都整体替换为掩码
    /// </summary>
    [Fact]
    public void MaskFieldValue_WhenSensitiveName_ReturnsMask()
    {
        Assert.Equal("***", Assert.IsType<string>(LogSanitizer.MaskFieldValue("Password", "plain-text")));
        Assert.Equal("***", Assert.IsType<string>(LogSanitizer.MaskFieldValue("Password", 42)));
    }

    /// <summary>
    /// 字段名不敏感（含空名字与元数据名）时原值直通，不做任何装箱转换
    /// </summary>
    [Fact]
    public void MaskFieldValue_WhenNotSensitiveName_ReturnsOriginalValue()
    {
        Assert.Equal(42, Assert.IsType<int>(LogSanitizer.MaskFieldValue("Amount", 42)));
        Assert.Equal("keep", Assert.IsType<string>(LogSanitizer.MaskFieldValue(null, "keep")));
        Assert.Equal("Bearer", Assert.IsType<string>(LogSanitizer.MaskFieldValue("TokenType", "Bearer")));
        Assert.Null(LogSanitizer.MaskFieldValue("Amount", null));
    }

    /// <summary>
    /// 空 JSON 原样返回
    /// </summary>
    /// <param name="json">原始 JSON</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskJsonFields_WhenNullOrWhiteSpace_ReturnsInput(string? json)
    {
        Assert.Equal(json, LogSanitizer.MaskJsonFields(json));
    }

    /// <summary>
    /// 字段级脱敏对任意值类型都生效：对象、数字、null 都会被整体掩掉
    /// </summary>
    /// <remarks>
    /// 这正是它相对 <c>MaskSensitiveData</c> 的存在意义——正则版掩不掉嵌套对象值。
    /// 断言走重新解析而非字符串比对，避免把序列化器的键序当契约。
    /// </remarks>
    [Fact]
    public void MaskJsonFields_WhenObjectRoot_MasksSensitiveKeysRegardlessOfValueKind()
    {
        var masked = LogSanitizer.MaskJsonFields(
            "{\"Password\":{\"Inner\":\"v\"},\"Amount\":42,\"Token\":null,\"Name\":\"tom\"}");

        Assert.NotNull(masked);

        using var document = JsonDocument.Parse(masked!);
        var root = document.RootElement;

        Assert.Equal("***", root.GetProperty("Password").GetString());
        Assert.Equal("***", root.GetProperty("Token").GetString());
        Assert.Equal(42, root.GetProperty("Amount").GetInt32());
        Assert.Equal("tom", root.GetProperty("Name").GetString());
    }

    /// <summary>
    /// 根节点不是对象时回落到正则脱敏，不原样吐出
    /// </summary>
    [Fact]
    public void MaskJsonFields_WhenArrayRoot_FallsBackToRegexMasking()
    {
        var masked = LogSanitizer.MaskJsonFields("[{\"password\":\"x\"}]");

        Assert.Equal("[{\"password\":\"***\"}]", masked);
    }

    /// <summary>
    /// JSON 解析失败时回落到正则脱敏，绝不把原文吐出去
    /// </summary>
    [Fact]
    public void MaskJsonFields_WhenInvalidJson_FallsBackToRegexMasking()
    {
        var masked = LogSanitizer.MaskJsonFields("password=abc&user=tom");

        Assert.Equal("password=***&user=tom", masked);
    }

    /// <summary>
    /// 请求头为空时返回空字典而不是 null，调用方无需判空
    /// </summary>
    [Fact]
    public void MaskHeaders_WhenNull_ReturnsEmptyDictionary()
    {
        var masked = LogSanitizer.MaskHeaders(null);

        Assert.Empty(masked);
    }

    /// <summary>
    /// 敏感头名整体掩码，其余头保留，且结果字典大小写不敏感
    /// </summary>
    [Fact]
    public void MaskHeaders_WhenSensitiveHeaderName_MasksWholeValue()
    {
        var headers = new List<KeyValuePair<string, string?>>
        {
            new("Authorization", "Bearer abc.def.ghi"),
            new("X-Api-Key", "k-123"),
            new("User-Agent", "Mozilla/5.0")
        };

        var masked = LogSanitizer.MaskHeaders(headers);

        Assert.Equal(3, masked.Count);
        Assert.Equal("***", masked["Authorization"]);
        Assert.Equal("***", masked["X-Api-Key"]);
        Assert.Equal("Mozilla/5.0", masked["User-Agent"]);

        // 头名大小写在传输层不稳定，查询必须大小写不敏感
        Assert.Equal("***", masked["authorization"]);
    }

    /// <summary>
    /// 头名不敏感但值里夹带秘密时，值仍会走一遍通用脱敏
    /// </summary>
    [Fact]
    public void MaskHeaders_WhenValueCarriesSecret_MasksValueContent()
    {
        var headers = new List<KeyValuePair<string, string?>>
        {
            new("X-Debug-Info", "password=123")
        };

        var masked = LogSanitizer.MaskHeaders(headers);

        Assert.Equal("password=***", masked["X-Debug-Info"]);
    }

    /// <summary>
    /// 头值为 null 时保持 null，不被替换成空串或掩码
    /// </summary>
    [Fact]
    public void MaskHeaders_WhenValueIsNull_KeepsNull()
    {
        var headers = new List<KeyValuePair<string, string?>>
        {
            new("X-Trace", null)
        };

        var masked = LogSanitizer.MaskHeaders(headers);

        Assert.Null(masked["X-Trace"]);
    }

    /// <summary>
    /// 仅大小写不同的重复头名会被合并为一条，后者覆盖前者
    /// </summary>
    [Fact]
    public void MaskHeaders_WhenDuplicateNamesDifferByCase_LastValueWins()
    {
        var headers = new List<KeyValuePair<string, string?>>
        {
            new("X-Trace", "first"),
            new("x-trace", "second")
        };

        var masked = LogSanitizer.MaskHeaders(headers);

        Assert.Equal("second", Assert.Single(masked).Value);
    }
}
