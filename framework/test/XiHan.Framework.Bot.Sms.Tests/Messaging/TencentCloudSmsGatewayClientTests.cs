// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Enums;
using XiHan.Framework.Bot.Sms.Messaging;

namespace XiHan.Framework.Bot.Sms.Tests.Messaging;

/// <summary>
/// <see cref="TencentCloudSmsGatewayClient"/> 腾讯云短信网关客户端测试
/// </summary>
/// <remarks>
/// 只覆盖到达腾讯云 SDK 之前的本地前置校验：入参非空、取消令牌、模板映射解析、位置参数数组拼装。
/// 腾讯云模板参数是位置数组，缺 paramOrder 或 paramOrder 声明的键在参数里找不到，
/// 都必须在触网前失败 —— 否则会发出一条参数错位的短信。全组用例零网络请求。
/// </remarks>
public class TencentCloudSmsGatewayClientTests
{
    private const string TemplateMapJson = """
        {
          "auth-sms-login-code": { "templateCode": "1234567", "paramOrder": [ "code" ] },
          "auth-sms-no-order": { "templateCode": "7654321" }
        }
        """;

    /// <summary>
    /// 服务商类型固定为腾讯云，供解析器与日志归因
    /// </summary>
    [Fact]
    public void Provider_IsTencentCloud()
    {
        var client = CreateClient();

        Assert.Equal(SmsProviderType.TencentCloud, client.Provider);
    }

    /// <summary>
    /// 客户端实现网关抽象，可直接被解析器返回
    /// </summary>
    [Fact]
    public void Type_ImplementsGatewayClientAbstraction()
    {
        var client = CreateClient();

        Assert.IsAssignableFrom<ISmsGatewayClient>(client);
        Assert.IsAssignableFrom<SmsGatewayClientBase>(client);
    }

    /// <summary>
    /// 发送请求为 null 时抛 ArgumentNullException，异常点名 request 参数
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var client = CreateClient();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await client.SendAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("request", exception.ParamName);
    }

    /// <summary>
    /// 令牌已取消时在触网前抛出取消异常
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenCancelled_ThrowsBeforeCallingProvider()
    {
        var client = CreateClient();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await client.SendAsync(CreateRequest("auth-sms-login-code"), cts.Token));
    }

    /// <summary>
    /// 缺少内部模板码时拒绝发送（云厂商必须按模板发送）
    /// </summary>
    /// <param name="templateCode">内部模板码</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WhenTemplateCodeBlank_Throws(string? templateCode)
    {
        var client = CreateClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.SendAsync(CreateRequest(templateCode), TestContext.Current.CancellationToken));

        Assert.Contains("TemplateCode", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 模板映射未覆盖该内部模板码时拒绝发送
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenTemplateNotMapped_Throws()
    {
        var client = CreateClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.SendAsync(CreateRequest("auth-sms-unknown"), TestContext.Current.CancellationToken));

        Assert.Contains("TemplateMap", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 有模板参数但映射未声明 paramOrder 时拒绝发送，异常点名 paramOrder
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenParamOrderMissing_Throws()
    {
        var client = CreateClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.SendAsync(CreateRequest("auth-sms-no-order"), TestContext.Current.CancellationToken));

        Assert.Contains("paramOrder", exception.Message, StringComparison.Ordinal);
        Assert.Contains("7654321", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// paramOrder 声明的键在模板参数中缺失时拒绝发送，避免发出参数错位的短信
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenParamOrderKeyMissingInParams_Throws()
    {
        var client = CreateClient();
        var request = new SmsGatewayRequest(["13800000000"], "auth-sms-login-code", """{"other":"1234"}""", "验证码 1234");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Contains("paramOrder", exception.Message, StringComparison.Ordinal);
        Assert.Contains("code", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 模板参数不是 JSON 对象时拒绝发送
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenTemplateParamsNotJsonObject_Throws()
    {
        var client = CreateClient();
        var request = new SmsGatewayRequest(["13800000000"], "auth-sms-login-code", "[1,2]", "验证码 1234");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Contains("JSON 对象", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 模板映射为空时任何模板码都拒绝发送
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenTemplateMapEmpty_Throws()
    {
        var client = new TencentCloudSmsGatewayClient(
            "test-secret-id", "test-secret-key", "1400000000", "ap-guangzhou", "曦寒科技", null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.SendAsync(CreateRequest("auth-sms-login-code"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 真实投递成功路径与逐号回执聚合需要可用的腾讯云凭据与网络，CI 不具备
    /// </summary>
    [Fact]
    public void SendAsync_SuccessPath_RequiresRealTencentCloudCredentials()
    {
        Assert.Skip("需要真实腾讯云短信凭据与网络，CI 不具备");
    }

    /// <summary>
    /// 构造一个带模板映射的腾讯云客户端（仅本地装配，不触网）
    /// </summary>
    /// <returns>腾讯云短信网关客户端</returns>
    private static TencentCloudSmsGatewayClient CreateClient()
    {
        return new TencentCloudSmsGatewayClient(
            "test-secret-id", "test-secret-key", "1400000000", "ap-guangzhou", "曦寒科技", TemplateMapJson);
    }

    /// <summary>
    /// 构造一条最简发送请求
    /// </summary>
    /// <param name="templateCode">内部模板码</param>
    /// <returns>发送请求</returns>
    private static SmsGatewayRequest CreateRequest(string? templateCode)
    {
        return new SmsGatewayRequest(["13800000000"], templateCode, """{"code":"1234"}""", "验证码 1234");
    }
}
