// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 代理一侧一次投递尝试的归类、去向、取消与日志
/// </summary>
/// <remarks>
/// 成功、返回失败、抛出、被停止取消各归成什么了结方式，失败后按尝试号与订阅方式的去向，应用不在服务状态时的等待，以及三条日志（RFC-0009「投递」第 3、5 条，ADR-0261）
/// </remarks>
public sealed class MessageDeliveryAttemptTests
{
    /// <summary>
    /// 测试主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 测试消息的 ID
    /// </summary>
    private const string MessageId = "order-1";

    /// <summary>
    /// 等待条件成立的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 最多投递 3 次的消息选项
    /// </summary>
    private static readonly MessagingOptions _options = new() { MaxAttempts = 3 };

    /// <summary>
    /// 处理器返回的业务错误
    /// </summary>
    private static readonly Error _rejected = Error.Literal("ORDERS_VALIDATION_001", "Rejected.");

    /// <summary>
    /// 给定尝试号与订阅方式的测试投递
    /// </summary>
    private static MessageDelivery Delivery(int attempt, SubscriptionMode mode = SubscriptionMode.Competing) => new()
    {
        Topic = Topic,
        Envelope = new MessageEnvelope { Id = MessageId, Source = "/tests", Type = "orders.placed" },
        Attempt = attempt,
        Mode = mode,
    };

    /// <summary>
    /// 总是返回给定结果的派发
    /// </summary>
    private static MessageDispatch Returning(Result result) => (_, _) => ValueTask.FromResult(result);

    /// <summary>
    /// 总是抛出给定异常的派发
    /// </summary>
    private static MessageDispatch Throwing(Exception exception) => (_, _) => throw exception;

    [Fact]
    public async Task Success_IsComplete_WithoutErrorOrLog()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();
        MessageDelivery? seen = null;
        var delivery = Delivery(1);

        var outcome = await MessageDeliveryAttempt.RunAsync(
            (dispatched, _) =>
            {
                seen = dispatched;
                return ValueTask.FromResult(Result.Success());
            },
            delivery,
            _options,
            host.App,
            logger,
            CancellationToken.None);

        // 派发收到的就是交进来的投递；成功不带错误，也不记日志
        Assert.Same(delivery, seen);
        Assert.Equal(DeliveryDisposition.Complete, outcome.Disposition);
        Assert.Null(outcome.Error);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(1, SubscriptionMode.Competing)]
    [InlineData(2, SubscriptionMode.Competing)]
    [InlineData(1, SubscriptionMode.Broadcast)]
    [InlineData(2, SubscriptionMode.Broadcast)]
    public async Task Failure_BelowMaxAttempts_IsRetry_WithTheHandlersError(int attempt, SubscriptionMode mode)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();

        var outcome = await MessageDeliveryAttempt.RunAsync(Returning(_rejected), Delivery(attempt, mode), _options, host.App, logger, CancellationToken.None);

        // 还有尝试次数：两种订阅方式都重投，带着处理器的错误；重投不记日志
        Assert.Equal(DeliveryDisposition.Retry, outcome.Disposition);
        Assert.Same(_rejected, outcome.Error);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Failure_AtOrPastTheLastAttempt_IsDeadLetterForCompeting_AndLogsIt(int attempt)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();

        var outcome = await MessageDeliveryAttempt.RunAsync(Returning(_rejected), Delivery(attempt), _options, host.App, logger, CancellationToken.None);

        // 尝试号由代理给出（RabbitMQ 的消息头），调小上限后可能已经超过：同样按用完处理
        Assert.Equal(DeliveryDisposition.DeadLetter, outcome.Disposition);
        Assert.Same(_rejected, outcome.Error);
        Assert.Equal(
            [(LogLevel.Warning, $"Message {MessageId} on topic {Topic} was dead-lettered after attempt {attempt} failed: ORDERS_VALIDATION_001")],
            logger.Entries.Select(static e => (e.Level, e.Message)));
    }

    [Fact]
    public async Task Failure_AtTheLastAttempt_IsDiscardForBroadcast_AndLogsIt()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();

        var outcome = await MessageDeliveryAttempt.RunAsync(Returning(_rejected), Delivery(3, SubscriptionMode.Broadcast), _options, host.App, logger, CancellationToken.None);

        // 广播没有死信：到上限丢弃并记日志
        Assert.Equal(DeliveryDisposition.Discard, outcome.Disposition);
        Assert.Same(_rejected, outcome.Error);
        Assert.Equal(
            [(LogLevel.Warning, $"Broadcast message {MessageId} on topic {Topic} was dropped after attempt 3 failed: ORDERS_VALIDATION_001")],
            logger.Entries.Select(static e => (e.Level, e.Message)));
    }

    [Fact]
    public async Task SingleAttempt_GoesStraightToTheDeadLetter()
    {
        await using var host = await TestHost.StartAsync(static _ => { });

        var outcome = await MessageDeliveryAttempt.RunAsync(Returning(_rejected), Delivery(1), new MessagingOptions { MaxAttempts = 1 }, host.App, NullLogger.Instance, CancellationToken.None);

        // 上限为 1：首次失败就是最后一次
        Assert.Equal(DeliveryDisposition.DeadLetter, outcome.Disposition);
    }

    [Fact]
    public async Task UninitializedResult_IsAFailedAttempt()
    {
        await using var host = await TestHost.StartAsync(static _ => { });

        var outcome = await MessageDeliveryAttempt.RunAsync(Returning(default), Delivery(1), _options, host.App, NullLogger.Instance, CancellationToken.None);

        // default 结果不是成功：按失败处理，错误是原语层的「未初始化」
        Assert.Equal(DeliveryDisposition.Retry, outcome.Disposition);
        Assert.Equal(ErrorCode.UninitializedResult, outcome.Error?.Code);
    }

    [Fact]
    public async Task Throwing_IsAFailedAttempt_WithTheExceptionTypeAsReason_AndLogsTheException()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();
        var thrown = new InvalidOperationException("Server=db-01;Password=secret");

        var outcome = await MessageDeliveryAttempt.RunAsync(Throwing(thrown), Delivery(2), _options, host.App, logger, CancellationToken.None);

        // 抛出与返回失败一样算一次失败的尝试；错误里只有异常类型，异常消息不进错误
        Assert.Equal(DeliveryDisposition.Retry, outcome.Disposition);
        var error = Assert.IsType<Error>(outcome.Error);
        Assert.Equal(MessagingErrors.HandlerFailed, error.Code);
        Assert.Equal(MessagingMessages.HandlerFailed, error.MessageKey);
        Assert.Equal("System.InvalidOperationException", error.Details?["reason"]);
        Assert.Null(error.Inner);
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);

        // 完整异常进日志：不记下来就没人看得到它
        var entry = Assert.Single(logger.Entries);
        Assert.Equal((LogLevel.Warning, $"Dispatching message {MessageId} on topic {Topic} threw on attempt 2."), (entry.Level, entry.Message));
        Assert.Same(thrown, entry.Exception);
    }

    [Fact]
    public async Task ThrowingAFrameworkException_KeepsItsCodeAsReason_AndItsErrorAsInner()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var thrown = new FrameworkException(KernelErrors.InvalidAppState, "The application is in state 'Draining' and cannot accept invocations.");

        var outcome = await MessageDeliveryAttempt.RunAsync(Throwing(thrown), Delivery(1), _options, host.App, NullLogger.Instance, CancellationToken.None);

        // 框架异常带着框架自己定的错误码：reason 写错误码，原错误留作内部错误
        Assert.Equal(DeliveryDisposition.Retry, outcome.Disposition);
        var error = Assert.IsType<Error>(outcome.Error);
        Assert.Equal(MessagingErrors.HandlerFailed, error.Code);
        Assert.Equal(KernelErrors.InvalidAppState, error.Details?["reason"]);
        Assert.Equal(KernelErrors.InvalidAppState, error.Inner?.Code);
    }

    [Theory]
    [InlineData(SubscriptionMode.Competing, DeliveryDisposition.DeadLetter, "was dead-lettered")]
    [InlineData(SubscriptionMode.Broadcast, DeliveryDisposition.Discard, "was dropped")]
    public async Task Throwing_AtTheLastAttempt_IsExhausted_WithTheHandlerFailedCode(SubscriptionMode mode, DeliveryDisposition expected, string phrase)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();

        var outcome = await MessageDeliveryAttempt.RunAsync(Throwing(new InvalidOperationException("Boom.")), Delivery(3, mode), _options, host.App, logger, CancellationToken.None);

        Assert.Equal(expected, outcome.Disposition);
        Assert.Equal(MessagingErrors.HandlerFailed, outcome.Error?.Code);
        // 先记抛出，再记了结：了结的那条带错误码
        Assert.Equal(2, logger.Entries.Count);
        Assert.Contains("threw on attempt 3", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains(phrase, logger.Entries[1].Message, StringComparison.Ordinal);
        Assert.EndsWith(": " + MessagingErrors.HandlerFailed, logger.Entries[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationException_WhenTheTokenIsCancelled_IsAbandon_AndNotLogged()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();
        using var stopping = new CancellationTokenSource();

        var outcome = await MessageDeliveryAttempt.RunAsync(
            async (_, cancellationToken) =>
            {
                await stopping.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
                return Result.Success();
            },
            Delivery(3),
            _options,
            host.App,
            logger,
            stopping.Token);

        // 停止打断了处理器：不算一次尝试，最后一次尝试上也不进死信
        Assert.Equal(DeliveryDisposition.Abandon, outcome.Disposition);
        Assert.Null(outcome.Error);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task CancellationException_WhenTheTokenIsNotCancelled_IsAFailedAttempt()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();

        var outcome = await MessageDeliveryAttempt.RunAsync(Throwing(new TaskCanceledException("The HTTP client timed out.")), Delivery(1), _options, host.App, logger, CancellationToken.None);

        // 令牌没有取消：这是处理器自己的超时，照常算一次失败
        Assert.Equal(DeliveryDisposition.Retry, outcome.Disposition);
        Assert.Equal(MessagingErrors.HandlerFailed, outcome.Error?.Code);
        Assert.Equal("System.Threading.Tasks.TaskCanceledException", outcome.Error?.Details?["reason"]);
        Assert.Single(logger.Entries);
    }

    [Theory]
    [InlineData(SubscriptionMode.Competing, false)]
    [InlineData(SubscriptionMode.Competing, true)]
    [InlineData(SubscriptionMode.Broadcast, false)]
    [InlineData(SubscriptionMode.Broadcast, true)]
    public async Task Failure_WhileTheTokenIsCancelled_IsAbandon_EvenAtTheLastAttempt(SubscriptionMode mode, bool throws)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logger = new RecordingLogger();
        using var stopping = new CancellationTokenSource();

        var outcome = await MessageDeliveryAttempt.RunAsync(
            async (_, _) =>
            {
                // 管道把处理器的取消写成失败结果返回（KERNEL_CANCELLED_001），不抛取消异常；释放中的作用域则可能抛出别的异常
                await stopping.CancelAsync();
                return throws ? throw new ObjectDisposedException("scope") : Result.Failure(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
            },
            Delivery(3, mode),
            _options,
            host.App,
            logger,
            stopping.Token);

        // 停止期间的失败不算一次尝试：不重投、不进死信、不丢弃，也不记进死信或丢弃的日志
        Assert.Equal(DeliveryDisposition.Abandon, outcome.Disposition);
        Assert.Null(outcome.Error);
        Assert.DoesNotContain(logger.Entries, static e => e.Message.Contains("dead-lettered", StringComparison.Ordinal) || e.Message.Contains("dropped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Success_WhileTheTokenIsCancelled_IsStillComplete()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        using var stopping = new CancellationTokenSource();

        var outcome = await MessageDeliveryAttempt.RunAsync(
            async (_, _) =>
            {
                await stopping.CancelAsync();
                return Result.Success();
            },
            Delivery(1),
            _options,
            host.App,
            NullLogger.Instance,
            stopping.Token);

        // 处理器已经处理完：停止信号到了也要确认，否则重启后再投一次
        Assert.Equal(DeliveryDisposition.Complete, outcome.Disposition);
    }

    [Fact]
    public async Task BeforeTheAppServes_TheDispatchWaits_AndRunsOnceItServes()
    {
        var dispatched = 0;
        var dispatchedWhileStarting = -1;
        var servingWhileStarting = true;
        Task<MessageDeliveryOutcome>? pending = null;
        // 特性启动期间应用还没进入服务状态：这时发起的尝试要等到启动完成才派发
        var probe = new StubFeature(TestManifests.Create("xihan.test.delivery-starting"))
        {
            OnStart = async (services, cancellationToken) =>
            {
                var app = services.GetRequiredService<App>();
                pending = MessageDeliveryAttempt.RunAsync(
                    (_, _) =>
                    {
                        Interlocked.Increment(ref dispatched);
                        return ValueTask.FromResult(Result.Success());
                    },
                    Delivery(1),
                    _options,
                    app,
                    NullLogger.Instance,
                    CancellationToken.None).AsTask();
                await Task.Delay(100, cancellationToken);
                servingWhileStarting = app.State.IsServing();
                dispatchedWhileStarting = Volatile.Read(ref dispatched);
            },
        };

        await using var host = await TestHost.StartAsync(b => b.UseFeature(probe));

        Assert.False(servingWhileStarting);
        Assert.Equal(0, dispatchedWhileStarting);
        Assert.NotNull(pending);
        Assert.Equal(DeliveryDisposition.Complete, (await pending.WaitAsync(_patience)).Disposition);
        Assert.Equal(1, dispatched);
    }

    [Fact]
    public async Task WhileTheAppDrains_TheDispatchWaits_AndStoppingAbandonsItWithoutDispatching()
    {
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // 特性停在闸门前，应用就一直处在排空中：代理的停止信号还没到
        var probe = new StubFeature(TestManifests.Create("xihan.test.delivery-draining"))
        {
            OnStop = async _ =>
            {
                stopEntered.SetResult();
                await stopGate.Task;
            },
        };
        await using var host = await TestHost.StartAsync(b => b.UseFeature(probe));
        var draining = host.App.StopAsync(CancellationToken.None);
        try
        {
            await stopEntered.Task.WaitAsync(_patience);
            Assert.Equal(LifecycleState.Draining, host.App.State);
            var dispatched = 0;
            var logger = new RecordingLogger();
            using var stopping = new CancellationTokenSource();

            // 最后一次尝试：排空中不派发，尝试次数不会被空转掉
            var pending = MessageDeliveryAttempt.RunAsync(
                (_, _) =>
                {
                    Interlocked.Increment(ref dispatched);
                    return ValueTask.FromResult(Result.Success());
                },
                Delivery(3),
                _options,
                host.App,
                logger,
                stopping.Token).AsTask();
            await Task.Delay(100);
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref dispatched));

            await stopping.CancelAsync();
            var outcome = await pending.WaitAsync(_patience);

            Assert.Equal(DeliveryDisposition.Abandon, outcome.Disposition);
            Assert.Equal(0, dispatched);
            Assert.Empty(logger.Entries);
        }
        finally
        {
            // 断言失败也要放开闸门，否则宿主停不下来
            stopGate.TrySetResult();
            await draining.WaitAsync(_patience);
        }
    }

    [Theory]
    [InlineData(SubscriptionMode.Competing, DeliveryDisposition.DeadLetter, "Message order-1 on topic orders was dead-lettered after attempt 4 failed: ?")]
    [InlineData(SubscriptionMode.Broadcast, DeliveryDisposition.Discard, "Broadcast message order-1 on topic orders was dropped after attempt 4 failed: ?")]
    public void Exhausted_WithoutAnError_SettlesByMode_AndLogsAQuestionMark(SubscriptionMode mode, DeliveryDisposition expected, string message)
    {
        var logger = new RecordingLogger();

        // 没有派发就判定用完（回收来的投递已经投过上限次数）：没有错误可带
        var outcome = MessageDeliveryAttempt.Exhausted(Delivery(4, mode), null, logger);

        Assert.Equal(expected, outcome.Disposition);
        Assert.Null(outcome.Error);
        Assert.Equal([(LogLevel.Warning, message)], logger.Entries.Select(static e => (e.Level, e.Message)));
    }

    [Fact]
    public void Exhausted_WithAnError_CarriesIt()
    {
        var logger = new RecordingLogger();

        // 还没到上限但没法再重投（内存代理的队列已满）：带着这次失败的错误了结
        var outcome = MessageDeliveryAttempt.Exhausted(Delivery(1), _rejected, logger);

        Assert.Equal(DeliveryDisposition.DeadLetter, outcome.Disposition);
        Assert.Same(_rejected, outcome.Error);
        Assert.EndsWith("after attempt 1 failed: ORDERS_VALIDATION_001", Assert.Single(logger.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultOutcome_IsAbandon_WithoutAnError()
    {
        // 没有结论的投递不能被当成已处理而确认掉
        MessageDeliveryOutcome outcome = default;

        Assert.Equal(DeliveryDisposition.Abandon, outcome.Disposition);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public async Task Arguments_AreValidated()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var dispatch = Returning(Result.Success());
        var delivery = Delivery(1);
        var logger = NullLogger.Instance;

        await Assert.ThrowsAsync<ArgumentNullException>("dispatch", async () => await MessageDeliveryAttempt.RunAsync(null!, delivery, _options, host.App, logger, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>("delivery", async () => await MessageDeliveryAttempt.RunAsync(dispatch, null!, _options, host.App, logger, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>("options", async () => await MessageDeliveryAttempt.RunAsync(dispatch, delivery, null!, host.App, logger, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>("app", async () => await MessageDeliveryAttempt.RunAsync(dispatch, delivery, _options, null!, logger, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>("logger", async () => await MessageDeliveryAttempt.RunAsync(dispatch, delivery, _options, host.App, null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>("delivery", () => MessageDeliveryAttempt.Exhausted(null!, null, logger));
        Assert.Throws<ArgumentNullException>("logger", () => MessageDeliveryAttempt.Exhausted(delivery, null, null!));
    }
}
