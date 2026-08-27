// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Models;
using XiHan.Framework.Messaging.Services;

namespace XiHan.Framework.Messaging.Tests;

/// <summary>
/// 未配置发送器兜底实现测试
/// </summary>
/// <remarks>
/// 这个类的全部价值就在于「明确失败」：它被无条件注册进 IMessageSender 集合，
/// 如果它返回成功结果，业务侧在完全没配邮件/短信通道的情况下也会以为消息已经发出去了。
/// 因此本组用例把「必须失败 + 错误信息里必须带上通道名」当作硬契约。
/// </remarks>
public class NotConfiguredMessageSenderTests
{
    /// <summary>
    /// 任意通道都被声明为可处理
    /// </summary>
    [Theory]
    [InlineData("email")]
    [InlineData("sms")]
    [InlineData("site")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("完全没见过的通道")]
    public void CanHandle_ForAnyChannel_ReturnsTrue(string channel)
    {
        var sender = new NotConfiguredMessageSender();

        Assert.True(sender.CanHandle(channel));
    }

    /// <summary>
    /// 发送结果必须是失败，且错误信息点名具体通道
    /// </summary>
    [Fact]
    public async Task SendAsync_Always_ReturnsFailureNamingChannel()
    {
        var sender = new NotConfiguredMessageSender();
        var envelope = new MessageEnvelope { Channel = "email" };
        var recipient = new MessageRecipient { Address = "a@x.com" };

        var result = await sender.SendAsync(envelope, recipient, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("email", result.ErrorMessage);
        Assert.Contains("未配置发送器", result.ErrorMessage);
        Assert.Null(result.ProviderMessageId);
    }

    /// <summary>
    /// 发送结果回填信封与接收人的标识信息
    /// </summary>
    [Fact]
    public async Task SendAsync_Always_CopiesEnvelopeAndRecipientIdentity()
    {
        var sender = new NotConfiguredMessageSender();
        var envelope = new MessageEnvelope { MessageId = "msg-7", Channel = "sms" };
        var recipient = new MessageRecipient { Address = "13800000000", DisplayName = "甲" };

        var result = await sender.SendAsync(envelope, recipient, TestContext.Current.CancellationToken);

        Assert.Equal("msg-7", result.MessageId);
        Assert.Equal("sms", result.Channel);
        Assert.Equal("13800000000", result.RecipientAddress);
    }

    /// <summary>
    /// 发送结果带有分发时间戳
    /// </summary>
    [Fact]
    public async Task SendAsync_Always_StampsDispatchedAt()
    {
        var sender = new NotConfiguredMessageSender();
        var before = DateTimeOffset.UtcNow;

        var result = await sender.SendAsync(new MessageEnvelope(), new MessageRecipient(), TestContext.Current.CancellationToken);

        Assert.NotEqual(default(DateTimeOffset), result.DispatchedAt);
        Assert.InRange(result.DispatchedAt, before, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 令牌已取消时抛出取消异常，而不是返回失败结果
    /// </summary>
    /// <remarks>
    /// 取消与业务失败是两种语义：取消必须冒泡让调用方感知，不能混进 ErrorMessage 里。
    /// </remarks>
    [Fact]
    public async Task SendAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceled()
    {
        var sender = new NotConfiguredMessageSender();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await sender.SendAsync(new MessageEnvelope(), new MessageRecipient(), cts.Token));
    }

    /// <summary>
    /// 兜底实现满足发送器抽象契约
    /// </summary>
    [Fact]
    public void Sender_ImplementsMessageSenderAbstraction()
    {
        Assert.IsAssignableFrom<IMessageSender>(new NotConfiguredMessageSender());
    }
}
