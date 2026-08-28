// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Models;
using XiHan.Framework.Messaging.Options;
using XiHan.Framework.Messaging.Services;
using XiHan.Framework.Messaging.Tests.Fakes;

namespace XiHan.Framework.Messaging.Tests.Services;

/// <summary>
/// 默认消息调度器测试
/// </summary>
/// <remarks>
/// 调度器本身不发消息，它的全部职责是「选发送器 + 逐接收人编排 + 回填结果标识 + 决定失败后是否继续」。
/// 因此这里不接任何真实通道，只用手写发送器替身把这四类编排契约钉死；
/// 其中「真实发送器优先于兜底发送器」和「兜底不会被误当成可用通道」是最容易被回归破坏的两条。
/// </remarks>
public class DefaultMessageDispatcherTests
{
    /// <summary>
    /// 信封为空时抛出参数异常
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenEnvelopeIsNull_ThrowsArgumentNullException()
    {
        var dispatcher = CreateDispatcher([new FakeMessageSender()]);

        await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(null!));
    }

    /// <summary>
    /// 通道为空白时抛出无效操作异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DispatchAsync_WhenChannelIsBlank_ThrowsInvalidOperationException(string channel)
    {
        var dispatcher = CreateDispatcher([new FakeMessageSender()]);
        var envelope = CreateEnvelope(channel, "a@x.com");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.Contains("消息通道", exception.Message);
    }

    /// <summary>
    /// 令牌已取消时抛出取消异常，不进入发送流程
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceled()
    {
        var sender = new FakeMessageSender();
        var dispatcher = CreateDispatcher([sender]);
        var envelope = CreateEnvelope("email", "a@x.com");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(envelope, cts.Token));

        Assert.Empty(sender.SentAddresses);
    }

    /// <summary>
    /// 没有接收人时直接返回空集合，且不做发送器匹配
    /// </summary>
    /// <remarks>
    /// 空接收人的短路发生在发送器解析之前，所以连 CanHandle 都不该被调用一次。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WhenNoRecipients_ReturnsEmptyWithoutResolvingSender()
    {
        var sender = new FakeMessageSender();
        var dispatcher = CreateDispatcher([sender]);
        var envelope = CreateEnvelope("email");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Empty(results);
        Assert.Empty(sender.CanHandleChannels);
        Assert.Empty(sender.SentAddresses);
    }

    /// <summary>
    /// 每个接收人产出一条结果，且顺序与接收人顺序一致
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WithMatchingSender_ReturnsOneResultPerRecipientInOrder()
    {
        var sender = new FakeMessageSender(handler: (_, recipient) => Task.FromResult(new MessageSendResult
        {
            IsSuccess = true,
            RecipientAddress = recipient.Address
        }));
        var dispatcher = CreateDispatcher([sender]);
        var envelope = CreateEnvelope("email", "a@x.com", "b@x.com", "c@x.com");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Equal(3, results.Count);
        Assert.Equal("a@x.com", results[0].RecipientAddress);
        Assert.Equal("b@x.com", results[1].RecipientAddress);
        Assert.Equal("c@x.com", results[2].RecipientAddress);
        Assert.Equal(3, sender.SentAddresses.Count);
        Assert.All(results, result => Assert.True(result.IsSuccess));
    }

    /// <summary>
    /// 发送器未填标识时由调度器用信封与接收人回填
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenSenderResultLacksIdentity_BackfillsFromEnvelope()
    {
        var sender = new FakeMessageSender(handler: (_, _) => Task.FromResult(new MessageSendResult { IsSuccess = true }));
        var dispatcher = CreateDispatcher([sender]);
        var envelope = CreateEnvelope("email", "a@x.com");
        envelope.MessageId = "msg-9";

        var result = Assert.Single(await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.Equal("msg-9", result.MessageId);
        Assert.Equal("email", result.Channel);
        Assert.Equal("a@x.com", result.RecipientAddress);
    }

    /// <summary>
    /// 发送器自带标识时调度器不覆盖
    /// </summary>
    /// <remarks>
    /// 第三方通道常常返回自己的消息号与实际落地通道，回填逻辑只能补空，不能改写。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WhenSenderResultCarriesIdentity_KeepsSenderValues()
    {
        var sender = new FakeMessageSender(handler: (_, _) => Task.FromResult(new MessageSendResult
        {
            MessageId = "provider-msg",
            Channel = "sms",
            RecipientAddress = "13800000000",
            ProviderMessageId = "p-1",
            IsSuccess = true
        }));
        var dispatcher = CreateDispatcher([sender]);
        var envelope = CreateEnvelope("email", "a@x.com");
        envelope.MessageId = "msg-9";

        var result = Assert.Single(await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.Equal("provider-msg", result.MessageId);
        Assert.Equal("sms", result.Channel);
        Assert.Equal("13800000000", result.RecipientAddress);
        Assert.Equal("p-1", result.ProviderMessageId);
    }

    /// <summary>
    /// 一个发送器都没有时，每个接收人各得一条失败结果
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenNoSenderRegistered_ReturnsFailurePerRecipient()
    {
        var dispatcher = CreateDispatcher([]);
        var envelope = CreateEnvelope("email", "a@x.com", "b@x.com");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(envelope.MessageId, result.MessageId);
            Assert.Equal("email", result.Channel);
            Assert.Contains("未找到可用发送器", result.ErrorMessage!);
        });
        Assert.Equal("a@x.com", results[0].RecipientAddress);
        Assert.Equal("b@x.com", results[1].RecipientAddress);
    }

    /// <summary>
    /// 一个发送器都没有时记录警告日志
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenNoSenderRegistered_LogsWarning()
    {
        var logger = new RecordingLogger<DefaultMessageDispatcher>();
        var dispatcher = CreateDispatcher([], logger: logger);

        await dispatcher.DispatchAsync(CreateEnvelope("email", "a@x.com"), TestContext.Current.CancellationToken);

        Assert.Contains(logger.Records, record => record.Level == LogLevel.Warning && record.Message.Contains("未找到可用发送器"));
    }

    /// <summary>
    /// 开启严格模式后找不到发送器直接抛异常
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenNoSenderAndThrowWhenNoSenderEnabled_ThrowsInvalidOperationException()
    {
        var dispatcher = CreateDispatcher([], new XiHanMessagingOptions { ThrowWhenNoSender = true });
        var envelope = CreateEnvelope("email", "a@x.com");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.Contains("未找到可用发送器", exception.Message);
        Assert.Contains("email", exception.Message);
    }

    /// <summary>
    /// 只挂着兜底发送器时返回失败结果，绝不静默成功
    /// </summary>
    /// <remarks>
    /// 这是整个模块最关键的一条契约：默认装配下唯一的发送器就是兜底实现，
    /// 若这里返回成功，未配置任何真实通道的系统会全链路「假装发送成功」。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WhenOnlyFallbackSenderRegistered_ReturnsFailureNotSilentSuccess()
    {
        var dispatcher = CreateDispatcher([new NotConfiguredMessageSender()]);
        var envelope = CreateEnvelope("email", "a@x.com");

        var result = Assert.Single(await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.False(result.IsSuccess);
        Assert.Contains("未配置发送器", result.ErrorMessage!);
        Assert.Equal(envelope.MessageId, result.MessageId);
        Assert.Equal("a@x.com", result.RecipientAddress);
    }

    /// <summary>
    /// 兜底发送器排在前面也不会遮蔽真实发送器
    /// </summary>
    /// <remarks>
    /// 兜底实现的 CanHandle 恒为 true，如果按注册顺序朴素取首个匹配项，真实通道就永远轮不到。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WhenFallbackRegisteredFirst_StillPrefersRealSender()
    {
        var real = new FakeMessageSender();
        var dispatcher = CreateDispatcher([new NotConfiguredMessageSender(), real]);
        var envelope = CreateEnvelope("email", "a@x.com");

        var result = Assert.Single(await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.True(result.IsSuccess);
        Assert.Equal("a@x.com", Assert.Single(real.SentAddresses));
    }

    /// <summary>
    /// 真实发送器不认该通道时才回落到兜底发送器
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenRealSenderRejectsChannel_FallsBackToNotConfiguredSender()
    {
        var real = new FakeMessageSender(canHandle: channel => channel == "sms");
        var dispatcher = CreateDispatcher([real, new NotConfiguredMessageSender()]);
        var envelope = CreateEnvelope("email", "a@x.com");

        var result = Assert.Single(await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.False(result.IsSuccess);
        Assert.Contains("未配置发送器", result.ErrorMessage!);
        Assert.Empty(real.SentAddresses);
    }

    /// <summary>
    /// 多个可匹配发送器时只使用第一个
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WithMultipleMatchingSenders_UsesFirstOnly()
    {
        var first = new FakeMessageSender();
        var second = new FakeMessageSender();
        var dispatcher = CreateDispatcher([first, second]);

        await dispatcher.DispatchAsync(CreateEnvelope("email", "a@x.com"), TestContext.Current.CancellationToken);

        Assert.Equal("a@x.com", Assert.Single(first.SentAddresses));
        Assert.Empty(second.SentAddresses);
    }

    /// <summary>
    /// 匹配发送器前会先裁掉通道两端空白
    /// </summary>
    [Fact]
    public async Task DispatchAsync_BeforeMatching_TrimsChannel()
    {
        var sender = new FakeMessageSender(canHandle: channel => channel == "email");
        var dispatcher = CreateDispatcher([sender]);
        var envelope = CreateEnvelope("  email  ", "a@x.com");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("email", Assert.Single(sender.CanHandleChannels));
    }

    /// <summary>
    /// 发送器同步抛异常时转成失败结果而非冒泡
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenSenderThrowsSynchronously_ConvertsExceptionToFailureResult()
    {
        var sender = new FakeMessageSender(handler: (_, _) => throw new InvalidOperationException("发送器故障"));
        var dispatcher = CreateDispatcher([sender]);
        var envelope = CreateEnvelope("email", "a@x.com");

        var result = Assert.Single(await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.False(result.IsSuccess);
        Assert.Equal("发送器故障", result.ErrorMessage);
        Assert.Equal(envelope.MessageId, result.MessageId);
        Assert.Equal("email", result.Channel);
        Assert.Equal("a@x.com", result.RecipientAddress);
    }

    /// <summary>
    /// 发送器返回失败任务时同样转成失败结果
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenSenderReturnsFaultedTask_ConvertsExceptionToFailureResult()
    {
        var sender = new FakeMessageSender(handler: (_, _) => Task.FromException<MessageSendResult>(new TimeoutException("通道超时")));
        var dispatcher = CreateDispatcher([sender]);
        var envelope = CreateEnvelope("email", "a@x.com");

        var result = Assert.Single(await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken));

        Assert.False(result.IsSuccess);
        Assert.Equal("通道超时", result.ErrorMessage);
    }

    /// <summary>
    /// 发送器抛异常时记录错误日志并带上原始异常
    /// </summary>
    /// <remarks>
    /// 异常被吞成失败结果后，原始堆栈只剩日志这一条出口，丢了就没法排障。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WhenSenderThrows_LogsErrorWithException()
    {
        var logger = new RecordingLogger<DefaultMessageDispatcher>();
        var sender = new FakeMessageSender(handler: (_, _) => throw new InvalidOperationException("发送器故障"));
        var dispatcher = CreateDispatcher([sender], logger: logger);

        await dispatcher.DispatchAsync(CreateEnvelope("email", "a@x.com"), TestContext.Current.CancellationToken);

        Assert.Contains(logger.Records, record => record.Level == LogLevel.Error && record.Error is InvalidOperationException);
    }

    /// <summary>
    /// 允许继续时单个接收人抛异常不影响后续接收人
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenSenderThrowsAndContinueOnError_KeepsSendingRemainingRecipients()
    {
        var sender = new FakeMessageSender(handler: (_, recipient) => recipient.Address == "a@x.com"
            ? throw new InvalidOperationException("第一个接收人失败")
            : Task.FromResult(new MessageSendResult { IsSuccess = true }));
        var dispatcher = CreateDispatcher([sender], new XiHanMessagingOptions { ContinueOnError = true });
        var envelope = CreateEnvelope("email", "a@x.com", "b@x.com");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsSuccess);
        Assert.True(results[1].IsSuccess);
        Assert.Equal(2, sender.SentAddresses.Count);
    }

    /// <summary>
    /// 不允许继续时首个接收人抛异常即中断
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenSenderThrowsAndNotContinueOnError_StopsAfterFirstRecipient()
    {
        var sender = new FakeMessageSender(handler: (_, _) => throw new InvalidOperationException("发送器故障"));
        var dispatcher = CreateDispatcher([sender], new XiHanMessagingOptions { ContinueOnError = false });
        var envelope = CreateEnvelope("email", "a@x.com", "b@x.com", "c@x.com");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("a@x.com", Assert.Single(sender.SentAddresses));
    }

    /// <summary>
    /// 不允许继续时首个失败结果即中断
    /// </summary>
    /// <remarks>
    /// 与上一条的区别在于发送器没抛异常、只是返回 IsSuccess=false，短路判断必须同样生效。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WhenSenderReturnsFailureAndNotContinueOnError_StopsAfterFirstFailure()
    {
        var sender = new FakeMessageSender(handler: (_, _) => Task.FromResult(new MessageSendResult
        {
            IsSuccess = false,
            ErrorMessage = "通道拒收"
        }));
        var dispatcher = CreateDispatcher([sender], new XiHanMessagingOptions { ContinueOnError = false });
        var envelope = CreateEnvelope("email", "a@x.com", "b@x.com", "c@x.com");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("a@x.com", Assert.Single(sender.SentAddresses));
    }

    /// <summary>
    /// 允许继续时全部失败也要把每个接收人都试一遍
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenSenderReturnsFailureAndContinueOnError_SendsToAllRecipients()
    {
        var sender = new FakeMessageSender(handler: (_, _) => Task.FromResult(new MessageSendResult
        {
            IsSuccess = false,
            ErrorMessage = "通道拒收"
        }));
        var dispatcher = CreateDispatcher([sender], new XiHanMessagingOptions { ContinueOnError = true });
        var envelope = CreateEnvelope("email", "a@x.com", "b@x.com", "c@x.com");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Equal(3, results.Count);
        Assert.Equal(3, sender.SentAddresses.Count);
        Assert.All(results, result => Assert.False(result.IsSuccess));
    }

    /// <summary>
    /// 不允许继续时全部成功仍要跑完所有接收人
    /// </summary>
    /// <remarks>
    /// 短路条件是「失败」而不是「发过一次」，成功路径不能被 ContinueOnError=false 误伤。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WhenAllSucceedAndNotContinueOnError_StillSendsToAllRecipients()
    {
        var sender = new FakeMessageSender();
        var dispatcher = CreateDispatcher([sender], new XiHanMessagingOptions { ContinueOnError = false });
        var envelope = CreateEnvelope("email", "a@x.com", "b@x.com", "c@x.com");

        var results = await dispatcher.DispatchAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Equal(3, results.Count);
        Assert.Equal(3, sender.SentAddresses.Count);
    }

    /// <summary>
    /// 取消令牌被原样透传给发送器
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ForwardsCancellationTokenToSender()
    {
        var sender = new FakeMessageSender();
        var dispatcher = CreateDispatcher([sender]);
        var token = TestContext.Current.CancellationToken;

        await dispatcher.DispatchAsync(CreateEnvelope("email", "a@x.com"), token);

        Assert.Equal(token, sender.LastCancellationToken);
    }

    /// <summary>
    /// 构造调度器
    /// </summary>
    private static DefaultMessageDispatcher CreateDispatcher(
        IMessageSender[] senders,
        XiHanMessagingOptions? options = null,
        RecordingLogger<DefaultMessageDispatcher>? logger = null)
    {
        return new DefaultMessageDispatcher(
            senders,
            global::Microsoft.Extensions.Options.Options.Create(options ?? new XiHanMessagingOptions()),
            logger ?? new RecordingLogger<DefaultMessageDispatcher>());
    }

    /// <summary>
    /// 构造指定通道与接收地址的信封
    /// </summary>
    private static MessageEnvelope CreateEnvelope(string channel, params string[] addresses)
    {
        return new MessageEnvelope
        {
            Channel = channel,
            Subject = "主题",
            Recipients = addresses.Select(address => new MessageRecipient { Address = address }).ToArray()
        };
    }
}
