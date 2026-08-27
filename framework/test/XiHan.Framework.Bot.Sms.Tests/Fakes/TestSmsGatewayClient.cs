// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Enums;
using XiHan.Framework.Bot.Sms.Messaging;

namespace XiHan.Framework.Bot.Sms.Tests.Fakes;

/// <summary>
/// <see cref="SmsGatewayClientBase"/> 的最小具体子类
/// </summary>
/// <remarks>
/// 抽象基类无法直接实例化，这里只把受保护的模板方法（签名、模板映射解析、参数解析）
/// 原样转成 public 供测试调用，不加任何额外逻辑，保证断言的是基类行为而非替身行为。
/// </remarks>
internal sealed class TestSmsGatewayClient : SmsGatewayClientBase
{
    /// <summary>
    /// 构造最小子类
    /// </summary>
    /// <param name="signName">短信签名</param>
    /// <param name="templateMapJson">模板映射 JSON</param>
    public TestSmsGatewayClient(string signName, string? templateMapJson)
        : base(signName, templateMapJson)
    {
    }

    /// <summary>
    /// 服务商类型，固定为阿里云仅为满足抽象成员
    /// </summary>
    public override SmsProviderType Provider => SmsProviderType.Aliyun;

    /// <summary>
    /// 暴露基类持有的短信签名
    /// </summary>
    public string ExposedSignName => SignName;

    /// <summary>
    /// 最后一次收到的发送请求
    /// </summary>
    public SmsGatewayRequest? LastRequest { get; private set; }

    /// <summary>
    /// 暴露基类的模板映射解析
    /// </summary>
    /// <param name="internalTemplateCode">内部模板码</param>
    /// <returns>服务商模板映射</returns>
    public SmsTemplateMapping ResolveMappingPublic(string? internalTemplateCode)
    {
        return ResolveMapping(internalTemplateCode);
    }

    /// <summary>
    /// 暴露基类的模板参数解析
    /// </summary>
    /// <param name="templateParamsJson">模板参数 JSON</param>
    /// <returns>参数键值字典</returns>
    public static Dictionary<string, string> ParseParamsPublic(string? templateParamsJson)
    {
        return ParseParams(templateParamsJson);
    }

    /// <summary>
    /// 记录请求并返回成功，不触碰网络
    /// </summary>
    /// <param name="request">发送请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>固定成功结果</returns>
    public override Task<SmsGatewaySendResult> SendAsync(SmsGatewayRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(new SmsGatewaySendResult(true, "test-message-id", null));
    }
}
