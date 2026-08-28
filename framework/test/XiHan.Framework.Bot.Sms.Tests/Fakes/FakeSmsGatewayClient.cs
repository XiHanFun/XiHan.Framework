// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Enums;

namespace XiHan.Framework.Bot.Sms.Tests.Fakes;

/// <summary>
/// 短信网关客户端手写替身
/// </summary>
/// <remarks>
/// 只记录入参并回放预置结果或异常，绝不触碰网络；
/// 用于验证 <c>SmsBotProvider</c> 的编排逻辑（入参组装、结果折叠、异常兜底）。
/// </remarks>
internal sealed class FakeSmsGatewayClient : ISmsGatewayClient
{
    /// <summary>
    /// 服务商类型
    /// </summary>
    public SmsProviderType Provider { get; set; } = SmsProviderType.Aliyun;

    /// <summary>
    /// 预置的发送结果
    /// </summary>
    public SmsGatewaySendResult Result { get; set; } = new(true, "fake-message-id", null);

    /// <summary>
    /// 预置的抛出异常，非 null 时优先于 <see cref="Result"/>
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>
    /// 最后一次收到的发送请求
    /// </summary>
    public SmsGatewayRequest? LastRequest { get; private set; }

    /// <summary>
    /// 最后一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 发送次数
    /// </summary>
    public int SendCount { get; private set; }

    /// <summary>
    /// 回放预置结果或异常
    /// </summary>
    /// <param name="request">发送请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预置的发送结果</returns>
    public Task<SmsGatewaySendResult> SendAsync(SmsGatewayRequest request, CancellationToken cancellationToken = default)
    {
        SendCount++;
        LastRequest = request;
        LastCancellationToken = cancellationToken;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(Result);
    }
}
