// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Enums;
using XiHan.Framework.Bot.Sms.Messaging;
using XiHan.Framework.Bot.Sms.Tests.Fakes;

namespace XiHan.Framework.Bot.Sms.Tests.Messaging;

/// <summary>
/// <see cref="SmsGatewayClientBase"/> 短信网关客户端基类测试
/// </summary>
/// <remarks>
/// 基类是抽象的，用 <see cref="TestSmsGatewayClient"/> 这个最小具体子类把受保护成员原样暴露出来断言。
/// 这里覆盖的是所有服务商客户端共用的两段纯逻辑：模板映射解析（缺映射即失败，fail-closed）
/// 与模板参数解析（非字符串取原始 JSON 文本）。全程不触碰网络。
/// </remarks>
public class SmsGatewayClientBaseTests
{
    private const string TemplateMapJson = """
        {
          "auth-sms-login-code": { "templateCode": "SMS_123456", "paramOrder": [ "code", "minutes" ] },
          "auth-sms-register": { "templateCode": "SMS_654321" }
        }
        """;

    /// <summary>
    /// 构造函数持有的短信签名原样透传给子类
    /// </summary>
    [Fact]
    public void SignName_IsCarriedFromConstructor()
    {
        var client = new TestSmsGatewayClient("曦寒科技", TemplateMapJson);

        Assert.Equal("曦寒科技", client.ExposedSignName);
    }

    /// <summary>
    /// 抽象的服务商类型由子类给出，基类不干预
    /// </summary>
    [Fact]
    public void Provider_IsProvidedBySubclass()
    {
        var client = new TestSmsGatewayClient("曦寒科技", null);

        Assert.Equal(SmsProviderType.Aliyun, client.Provider);
    }

    /// <summary>
    /// 抽象的发送方法由子类实现，基类不拦截请求
    /// </summary>
    [Fact]
    public async Task SendAsync_IsDelegatedToSubclass()
    {
        var client = new TestSmsGatewayClient("曦寒科技", TemplateMapJson);
        var request = new SmsGatewayRequest(["13800000000"], "auth-sms-register", null, "内容");

        var result = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(request, client.LastRequest);
    }

    /// <summary>
    /// 模板码为空白时拒绝发送，异常点名 TemplateCode
    /// </summary>
    /// <param name="internalTemplateCode">内部模板码</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveMapping_WhenTemplateCodeBlank_Throws(string? internalTemplateCode)
    {
        var client = new TestSmsGatewayClient("曦寒科技", TemplateMapJson);

        var exception = Assert.Throws<InvalidOperationException>(() => client.ResolveMappingPublic(internalTemplateCode));

        Assert.Contains("TemplateCode", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 模板映射里没有该内部模板码时拒绝发送，异常提示补 TemplateMap
    /// </summary>
    [Fact]
    public void ResolveMapping_WhenCodeNotMapped_Throws()
    {
        var client = new TestSmsGatewayClient("曦寒科技", TemplateMapJson);

        var exception = Assert.Throws<InvalidOperationException>(() => client.ResolveMappingPublic("auth-sms-unknown"));

        Assert.Contains("auth-sms-unknown", exception.Message, StringComparison.Ordinal);
        Assert.Contains("TemplateMap", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 命中映射时返回服务商模板码与参数顺序
    /// </summary>
    [Fact]
    public void ResolveMapping_WhenCodeMapped_ReturnsProviderTemplate()
    {
        var client = new TestSmsGatewayClient("曦寒科技", TemplateMapJson);

        var mapping = client.ResolveMappingPublic("auth-sms-login-code");

        Assert.Equal("SMS_123456", mapping.TemplateCode);
        Assert.NotNull(mapping.ParamOrder);
        Assert.Equal(new[] { "code", "minutes" }, mapping.ParamOrder!);
    }

    /// <summary>
    /// 未声明 paramOrder 的映射项其参数顺序为 null（阿里云按命名参数发送不需要）
    /// </summary>
    [Fact]
    public void ResolveMapping_WhenParamOrderOmitted_IsNull()
    {
        var client = new TestSmsGatewayClient("曦寒科技", TemplateMapJson);

        var mapping = client.ResolveMappingPublic("auth-sms-register");

        Assert.Equal("SMS_654321", mapping.TemplateCode);
        Assert.Null(mapping.ParamOrder);
    }

    /// <summary>
    /// 内部模板码大小写不敏感，且前后空白被裁剪
    /// </summary>
    /// <param name="internalTemplateCode">调用方传入的内部模板码</param>
    [Theory]
    [InlineData("AUTH-SMS-LOGIN-CODE")]
    [InlineData("Auth-Sms-Login-Code")]
    [InlineData("  auth-sms-login-code  ")]
    public void ResolveMapping_IsCaseInsensitiveAndTrimmed(string internalTemplateCode)
    {
        var client = new TestSmsGatewayClient("曦寒科技", TemplateMapJson);

        var mapping = client.ResolveMappingPublic(internalTemplateCode);

        Assert.Equal("SMS_123456", mapping.TemplateCode);
    }

    /// <summary>
    /// 映射项的 JSON 属性名大小写不敏感，帕斯卡命名同样能解析
    /// </summary>
    [Fact]
    public void ResolveMapping_AcceptsPascalCaseJsonPropertyNames()
    {
        var client = new TestSmsGatewayClient("曦寒科技", """{"login":{"TemplateCode":"SMS_1","ParamOrder":["code"]}}""");

        var mapping = client.ResolveMappingPublic("login");

        Assert.Equal("SMS_1", mapping.TemplateCode);
        Assert.Equal(new[] { "code" }, mapping.ParamOrder!);
    }

    /// <summary>
    /// 模板映射为空白时得到空映射表，任何模板码都视为未配置
    /// </summary>
    /// <param name="templateMapJson">模板映射 JSON</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveMapping_WhenTemplateMapBlank_AlwaysThrows(string? templateMapJson)
    {
        var client = new TestSmsGatewayClient("曦寒科技", templateMapJson);

        Assert.Throws<InvalidOperationException>(() => client.ResolveMappingPublic("auth-sms-login-code"));
    }

    /// <summary>
    /// 模板映射 JSON 字面量为 null 时构造即失败，不允许带着空映射静默上线
    /// </summary>
    [Fact]
    public void Ctor_WhenTemplateMapJsonIsNullLiteral_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new TestSmsGatewayClient("曦寒科技", "null"));

        Assert.Contains("模板映射", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 模板映射 JSON 结构不是对象时构造即失败
    /// </summary>
    [Fact]
    public void Ctor_WhenTemplateMapJsonIsArray_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => new TestSmsGatewayClient("曦寒科技", "[1,2]"));
    }

    /// <summary>
    /// 模板参数为空白时得到空字典，而不是抛异常（无参模板是合法场景）
    /// </summary>
    /// <param name="templateParamsJson">模板参数 JSON</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseParams_WhenBlank_ReturnsEmpty(string? templateParamsJson)
    {
        var result = TestSmsGatewayClient.ParseParamsPublic(templateParamsJson);

        Assert.Empty(result);
    }

    /// <summary>
    /// 字符串值原样取出
    /// </summary>
    [Fact]
    public void ParseParams_WithStringValues_TakesStringContent()
    {
        var result = TestSmsGatewayClient.ParseParamsPublic("""{"code":"1234","name":"曦寒"}""");

        Assert.Equal(2, result.Count);
        Assert.Equal("1234", result["code"]);
        Assert.Equal("曦寒", result["name"]);
    }

    /// <summary>
    /// 非字符串值取原始 JSON 文本（数字不带引号、布尔为小写、null 为字面量 null）
    /// </summary>
    [Fact]
    public void ParseParams_WithNonStringValues_TakesRawJsonText()
    {
        var result = TestSmsGatewayClient.ParseParamsPublic("""{"count":12,"flag":true,"nothing":null}""");

        Assert.Equal("12", result["count"]);
        Assert.Equal("true", result["flag"]);
        Assert.Equal("null", result["nothing"]);
    }

    /// <summary>
    /// 嵌套对象值取整段原始 JSON 文本
    /// </summary>
    [Fact]
    public void ParseParams_WithNestedObject_TakesRawJsonText()
    {
        var result = TestSmsGatewayClient.ParseParamsPublic("""{"nested":{"a":1}}""");

        Assert.Contains("\"a\"", result["nested"], StringComparison.Ordinal);
        Assert.Contains("1", result["nested"], StringComparison.Ordinal);
    }

    /// <summary>
    /// 参数字典按大小写不敏感检索，方便与服务商模板变量名对齐
    /// </summary>
    [Fact]
    public void ParseParams_ResultLookup_IsCaseInsensitive()
    {
        var result = TestSmsGatewayClient.ParseParamsPublic("""{"Code":"1234"}""");

        Assert.True(result.ContainsKey("code"));
        Assert.Equal("1234", result["CODE"]);
    }

    /// <summary>
    /// 模板参数不是 JSON 对象时明确报错，而不是静默丢参数
    /// </summary>
    /// <param name="templateParamsJson">模板参数 JSON</param>
    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"just-a-string\"")]
    [InlineData("123")]
    public void ParseParams_WhenNotJsonObject_Throws(string templateParamsJson)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TestSmsGatewayClient.ParseParamsPublic(templateParamsJson));

        Assert.Contains("JSON 对象", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 模板参数不是合法 JSON 时抛 JsonException
    /// </summary>
    [Fact]
    public void ParseParams_WhenMalformedJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => TestSmsGatewayClient.ParseParamsPublic("{oops"));
    }
}
