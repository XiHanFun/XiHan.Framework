// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Sms.Messaging;
using XiHan.Framework.Bot.Sms.Options;
using XiHan.Framework.Bot.Sms.Tests.Fakes;

namespace XiHan.Framework.Bot.Sms.Tests.Messaging;

/// <summary>
/// <see cref="SmsBotProvider"/> 短信 Bot 提供者测试
/// </summary>
/// <remarks>
/// 提供者本身不发短信，只做三件事：从 BotMessage.Data 里解析收件人与模板信息、
/// 向解析器要网关（要不到就 fail-closed 返回 BadRequest）、把网关结果折叠成 BotResult。
/// 网关与解析器全部用手写替身，全程零网络请求。
/// </remarks>
public class SmsBotProviderTests
{
    /// <summary>
    /// 提供者名称固定为 Sms，与 BotProviderNames 常量一致
    /// </summary>
    [Fact]
    public void Name_IsSmsProviderName()
    {
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(new FakeSmsGatewayClient()));

        Assert.Equal(BotProviderNames.Sms, provider.Name);
        Assert.Equal("Sms", provider.Name);
    }

    /// <summary>
    /// 提供者实现 IBotProvider，可被 Bot 调度器统一编排
    /// </summary>
    [Fact]
    public void Type_ImplementsBotProviderAbstraction()
    {
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(null));

        Assert.IsAssignableFrom<IBotProvider>(provider);
    }

    /// <summary>
    /// 消息里没有手机号数据时直接返回 BadRequest，且不去解析网关
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenPhoneDataMissing_ReturnsBadRequestWithoutResolving()
    {
        var resolver = new FakeSmsGatewayResolver(new FakeSmsGatewayClient());
        var provider = new SmsBotProvider(resolver);
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("Sms recipients are required.", result.Message);
        Assert.Equal(BotProviderNames.Sms, result.Provider);
        Assert.Equal(0, resolver.ResolveCount);
    }

    /// <summary>
    /// 手机号为空白字符串时视为无收件人
    /// </summary>
    /// <param name="phoneNumbers">手机号数据</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData(" , , ")]
    public async Task SendAsync_WhenPhoneStringIsBlank_ReturnsBadRequest(string phoneNumbers)
    {
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(new FakeSmsGatewayClient()));
        var message = CreateMessage();
        message.Data[SmsMessageDataKeys.PhoneNumbers] = phoneNumbers;

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("Sms recipients are required.", result.Message);
    }

    /// <summary>
    /// 手机号集合全为空白项时视为无收件人
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenPhoneListAllBlank_ReturnsBadRequest()
    {
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(new FakeSmsGatewayClient()));
        var message = CreateMessage();
        message.Data[SmsMessageDataKeys.PhoneNumbers] = new List<string> { string.Empty, "   " };

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
    }

    /// <summary>
    /// 手机号数据类型不受支持（既非字符串也非字符串集合）时视为无收件人
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenPhoneDataTypeUnsupported_ReturnsBadRequest()
    {
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(new FakeSmsGatewayClient()));
        var message = CreateMessage();
        message.Data[SmsMessageDataKeys.PhoneNumbers] = 13800000000L;

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
    }

    /// <summary>
    /// 逗号分隔的手机号被拆分、裁剪空白并剔除空项
    /// </summary>
    [Fact]
    public async Task SendAsync_WithCommaSeparatedPhones_SplitsTrimsAndDropsEmpty()
    {
        var client = new FakeSmsGatewayClient();
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessage();
        message.Data[SmsMessageDataKeys.PhoneNumbers] = "13800000000, 13900000000 ,";

        await provider.SendAsync(message, CreateContext(message));

        Assert.NotNull(client.LastRequest);
        Assert.Equal(2, client.LastRequest!.PhoneNumbers.Count);
        Assert.Equal("13800000000", client.LastRequest.PhoneNumbers[0]);
        Assert.Equal("13900000000", client.LastRequest.PhoneNumbers[1]);
    }

    /// <summary>
    /// 字符串集合形式的手机号剔除空白项后原样透传
    /// </summary>
    [Fact]
    public async Task SendAsync_WithStringCollectionPhones_FiltersBlankEntries()
    {
        var client = new FakeSmsGatewayClient();
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessage();
        message.Data[SmsMessageDataKeys.PhoneNumbers] = new[] { "13800000000", string.Empty, "   ", "13900000000" };

        await provider.SendAsync(message, CreateContext(message));

        Assert.NotNull(client.LastRequest);
        Assert.Equal(2, client.LastRequest!.PhoneNumbers.Count);
        Assert.Equal("13800000000", client.LastRequest.PhoneNumbers[0]);
        Assert.Equal("13900000000", client.LastRequest.PhoneNumbers[1]);
    }

    /// <summary>
    /// Data 键名大小写不敏感，调用方写成小写同样能取到手机号
    /// </summary>
    [Fact]
    public async Task SendAsync_MessageDataKeys_AreCaseInsensitive()
    {
        var client = new FakeSmsGatewayClient();
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessage();
        message.Data["sms.phonenumbers"] = "13800000000";
        message.Data["sms.templatecode"] = "auth-sms-login-code";

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.Success, result.Code);
        Assert.NotNull(client.LastRequest);
        Assert.Equal("auth-sms-login-code", client.LastRequest!.TemplateCode);
    }

    /// <summary>
    /// 解析不到网关（未配置或已禁用）时 fail-closed 返回 BadRequest，绝不静默假成功
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenGatewayNotResolved_ReturnsBadRequest()
    {
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(null));
        var message = CreateMessageWithPhone();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("Sms provider is not configured or disabled.", result.Message);
        Assert.Equal(BotProviderNames.Sms, result.Provider);
    }

    /// <summary>
    /// 网关成功时返回成功结果，回执ID放进 Data
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenGatewaySucceeds_ReturnsSuccessWithProviderMessageId()
    {
        var client = new FakeSmsGatewayClient { Result = new(true, "biz-1,biz-2", null) };
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessageWithPhone();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.True(result.IsSuccess);
        Assert.Equal(BotResultCodes.Success, result.Code);
        Assert.Equal("biz-1,biz-2", Assert.IsType<string>(result.Data));
        Assert.Equal(BotProviderNames.Sms, result.Provider);
    }

    /// <summary>
    /// 网关失败时返回失败结果并带上网关错误信息
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenGatewayFails_ReturnsFailedWithGatewayMessage()
    {
        var client = new FakeSmsGatewayClient { Result = new(false, null, "阿里云短信发送失败：isv.SMS_SIGNATURE_ILLEGAL-签名不合法") };
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessageWithPhone();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.Failed, result.Code);
        Assert.Equal("阿里云短信发送失败：isv.SMS_SIGNATURE_ILLEGAL-签名不合法", result.Message);
        Assert.Equal(BotProviderNames.Sms, result.Provider);
    }

    /// <summary>
    /// 网关失败但没给错误信息时回落到统一兜底文案
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenGatewayFailsWithoutMessage_ReturnsDefaultFailureMessage()
    {
        var client = new FakeSmsGatewayClient { Result = new(false, null, null) };
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessageWithPhone();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.Failed, result.Code);
        Assert.Equal("Sms send failed.", result.Message);
    }

    /// <summary>
    /// 网关抛异常（模板映射缺失、参数不合法、SDK 异常）折叠为失败结果，不打断多提供者调度
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenGatewayThrows_FoldsIntoFailedResult()
    {
        var client = new FakeSmsGatewayClient
        {
            ExceptionToThrow = new InvalidOperationException("短信网关配置的模板映射缺少内部模板码")
        };
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessageWithPhone();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.Failed, result.Code);
        Assert.Equal("短信网关配置的模板映射缺少内部模板码", result.Message);
        Assert.Equal(BotProviderNames.Sms, result.Provider);
    }

    /// <summary>
    /// 取消异常不被折叠，原样向上冒泡，保留调度侧的取消语义
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenGatewayThrowsOperationCanceled_Propagates()
    {
        var client = new FakeSmsGatewayClient { ExceptionToThrow = new OperationCanceledException() };
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessageWithPhone();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await provider.SendAsync(message, CreateContext(message)));
    }

    /// <summary>
    /// 模板码、模板参数与已渲染内容原样组装进网关请求
    /// </summary>
    [Fact]
    public async Task SendAsync_ComposesGatewayRequestFromMessage()
    {
        var client = new FakeSmsGatewayClient();
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessageWithPhone();
        message.Content = "验证码 1234，5 分钟内有效";
        message.Data[SmsMessageDataKeys.TemplateCode] = "auth-sms-login-code";
        message.Data[SmsMessageDataKeys.TemplateParams] = """{"code":"1234","minutes":"5"}""";

        await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(1, client.SendCount);
        Assert.NotNull(client.LastRequest);
        Assert.Equal("auth-sms-login-code", client.LastRequest!.TemplateCode);
        Assert.Equal("""{"code":"1234","minutes":"5"}""", client.LastRequest.TemplateParamsJson);
        Assert.Equal("验证码 1234，5 分钟内有效", client.LastRequest.Content);
    }

    /// <summary>
    /// 缺少模板码与模板参数时以 null 传递，由网关客户端按各自规则报错
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenTemplateDataMissing_PassesNulls()
    {
        var client = new FakeSmsGatewayClient();
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessageWithPhone();

        await provider.SendAsync(message, CreateContext(message));

        Assert.NotNull(client.LastRequest);
        Assert.Null(client.LastRequest!.TemplateCode);
        Assert.Null(client.LastRequest.TemplateParamsJson);
    }

    /// <summary>
    /// 模板码数据类型不是字符串时按缺失处理，不做隐式转换
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenTemplateDataTypeMismatch_PassesNulls()
    {
        var client = new FakeSmsGatewayClient();
        var provider = new SmsBotProvider(new FakeSmsGatewayResolver(client));
        var message = CreateMessageWithPhone();
        message.Data[SmsMessageDataKeys.TemplateCode] = 123456;
        message.Data[SmsMessageDataKeys.TemplateParams] = new Dictionary<string, string> { ["code"] = "1234" };

        await provider.SendAsync(message, CreateContext(message));

        Assert.NotNull(client.LastRequest);
        Assert.Null(client.LastRequest!.TemplateCode);
        Assert.Null(client.LastRequest.TemplateParamsJson);
    }

    /// <summary>
    /// 上下文取消令牌同时透传给解析器与网关客户端
    /// </summary>
    [Fact]
    public async Task SendAsync_PassesContextCancellationTokenDownstream()
    {
        var client = new FakeSmsGatewayClient();
        var resolver = new FakeSmsGatewayResolver(client);
        var provider = new SmsBotProvider(resolver);
        var message = CreateMessageWithPhone();
        using var cts = new CancellationTokenSource();

        await provider.SendAsync(message, CreateContext(message, cts.Token));

        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal(cts.Token, resolver.LastCancellationToken);
        Assert.Equal(cts.Token, client.LastCancellationToken);
    }

    /// <summary>
    /// 构造一条最简消息
    /// </summary>
    /// <returns>Bot 消息</returns>
    private static BotMessage CreateMessage()
    {
        return new BotMessage
        {
            Title = "验证码",
            Content = "验证码 1234"
        };
    }

    /// <summary>
    /// 构造一条带单个手机号的消息
    /// </summary>
    /// <returns>Bot 消息</returns>
    private static BotMessage CreateMessageWithPhone()
    {
        var message = CreateMessage();
        message.Data[SmsMessageDataKeys.PhoneNumbers] = "13800000000";
        return message;
    }

    /// <summary>
    /// 构造调度上下文
    /// </summary>
    /// <param name="message">Bot 消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度上下文</returns>
    private static BotContext CreateContext(BotMessage message, CancellationToken cancellationToken = default)
    {
        return new BotContext(message, [BotProviderNames.Sms], cancellationToken);
    }
}
