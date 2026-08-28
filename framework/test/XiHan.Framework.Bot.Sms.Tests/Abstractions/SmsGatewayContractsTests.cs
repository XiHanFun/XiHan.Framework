// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Abstractions;

namespace XiHan.Framework.Bot.Sms.Tests.Abstractions;

/// <summary>
/// <see cref="SmsGatewayRequest"/> 与 <see cref="SmsGatewaySendResult"/> 网关契约测试
/// </summary>
/// <remarks>
/// 两者都是位置记录，构成 SmsBotProvider 与各服务商客户端之间的唯一数据契约；
/// 这里锁定参数顺序（解构次序）、可空性与记录的值相等语义。
/// </remarks>
public class SmsGatewayContractsTests
{
    /// <summary>
    /// 请求按 手机号-模板码-模板参数-内容 的位置顺序构造并原样读回
    /// </summary>
    [Fact]
    public void Request_PositionalOrder_IsPhonesTemplateParamsContent()
    {
        var phones = new[] { "13800000000", "13900000000" };

        var request = new SmsGatewayRequest(phones, "auth-login", """{"code":"1234"}""", "验证码 1234");

        Assert.Same(phones, request.PhoneNumbers);
        Assert.Equal("auth-login", request.TemplateCode);
        Assert.Equal("""{"code":"1234"}""", request.TemplateParamsJson);
        Assert.Equal("验证码 1234", request.Content);
    }

    /// <summary>
    /// 模板码与模板参数可空，内容非空
    /// </summary>
    [Fact]
    public void Request_TemplateCodeAndParams_AreNullable()
    {
        var request = new SmsGatewayRequest([], null, null, string.Empty);

        Assert.Empty(request.PhoneNumbers);
        Assert.Null(request.TemplateCode);
        Assert.Null(request.TemplateParamsJson);
        Assert.Equal(string.Empty, request.Content);
    }

    /// <summary>
    /// 请求支持解构，解构次序与位置参数一致
    /// </summary>
    [Fact]
    public void Request_Deconstruct_MatchesPositionalOrder()
    {
        var request = new SmsGatewayRequest(["13800000000"], "auth-login", """{"code":"1"}""", "内容");

        var (phones, templateCode, paramsJson, content) = request;

        Assert.Single(phones);
        Assert.Equal("13800000000", phones[0]);
        Assert.Equal("auth-login", templateCode);
        Assert.Equal("""{"code":"1"}""", paramsJson);
        Assert.Equal("内容", content);
    }

    /// <summary>
    /// 请求为记录类型，引用同一手机号集合且其余字段相同时值相等
    /// </summary>
    [Fact]
    public void Request_ValueEquality_HoldsForSameComponents()
    {
        var phones = new[] { "13800000000" };

        var left = new SmsGatewayRequest(phones, "auth-login", null, "内容");
        var right = new SmsGatewayRequest(phones, "auth-login", null, "内容");

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// 请求的 with 表达式产出新实例且只改动指定成员
    /// </summary>
    [Fact]
    public void Request_WithExpression_CopiesAndOverridesOnlyTarget()
    {
        var original = new SmsGatewayRequest(["13800000000"], "auth-login", null, "内容");

        var modified = original with { TemplateCode = "auth-register" };

        Assert.NotSame(original, modified);
        Assert.Equal("auth-register", modified.TemplateCode);
        Assert.Equal("auth-login", original.TemplateCode);
        Assert.Same(original.PhoneNumbers, modified.PhoneNumbers);
        Assert.Equal(original.Content, modified.Content);
        Assert.NotEqual(original, modified);
    }

    /// <summary>
    /// 成功结果携带回执ID、无错误信息
    /// </summary>
    [Fact]
    public void SendResult_Success_CarriesProviderMessageId()
    {
        var result = new SmsGatewaySendResult(true, "biz-1,biz-2", null);

        Assert.True(result.IsSuccess);
        Assert.Equal("biz-1,biz-2", result.ProviderMessageId);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// 失败结果携带错误信息、无回执ID
    /// </summary>
    [Fact]
    public void SendResult_Failure_CarriesErrorMessage()
    {
        var result = new SmsGatewaySendResult(false, null, "网关拒绝");

        Assert.False(result.IsSuccess);
        Assert.Null(result.ProviderMessageId);
        Assert.Equal("网关拒绝", result.ErrorMessage);
    }

    /// <summary>
    /// 结果为记录类型，成员相同即值相等；成功标志不同即不相等
    /// </summary>
    [Fact]
    public void SendResult_ValueEquality_FollowsComponents()
    {
        var success = new SmsGatewaySendResult(true, "biz-1", null);

        Assert.Equal(new SmsGatewaySendResult(true, "biz-1", null), success);
        Assert.NotEqual(new SmsGatewaySendResult(false, "biz-1", null), success);
        Assert.NotEqual(new SmsGatewaySendResult(true, "biz-2", null), success);
    }
}
