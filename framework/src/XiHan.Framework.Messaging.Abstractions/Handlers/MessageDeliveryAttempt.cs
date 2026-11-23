// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 代理一侧的一次投递尝试：等应用进入服务状态、派发一次、把结果归成了结方式
/// </summary>
/// <remarks>
/// 各代理的消费循环差别很大（重新入队、重新发布、原位重试），相同的只有这一段，收在这里一份（RFC-0009「投递」第 3、5 条，ADR-0261）。代理只把 <see cref="MessageDeliveryOutcome.Disposition"/> 翻译成自己的确认、重投与死信动作
/// <para>
/// 每次尝试之前都等应用进入服务状态，不只在一条消息的第一次：原位重试的代理在应用排空期间接着派发，管道只会拒绝调用，几次空转就把尝试次数用完
/// </para>
/// <para>
/// 派发返回成功即 <see cref="DeliveryDisposition.Complete"/>。返回失败与抛出异常都算一次失败的尝试：抛出的异常转成 <see cref="MessagingErrors.HandlerFailed"/>，<c>reason</c> 只写异常类型的全名（<see cref="FrameworkException"/> 写它的错误码，原错误作内部错误），完整异常经 <c>logger</c> 记警告。失败之后尝试号小于 <see cref="MessagingOptions.MaxAttempts"/> 即 <see cref="DeliveryDisposition.Retry"/>，否则按 <see cref="Exhausted"/> 了结
/// </para>
/// <para>
/// 取消只认令牌：令牌已请求取消时抛出的 <see cref="OperationCanceledException"/>，以及令牌已请求取消时返回的失败或抛出的其他异常（管道把取消写成失败结果返回，不抛异常），都是 <see cref="DeliveryDisposition.Abandon"/>，不算一次尝试。令牌没有取消时的 <see cref="OperationCanceledException"/>（处理器自己的超时）是一次失败
/// </para>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static partial class MessageDeliveryAttempt
{
    /// <summary>
    /// 没有错误可记时日志里的错误码
    /// </summary>
    private const string UnknownErrorCode = "?";

    /// <summary>
    /// 等应用进入服务状态时两次查看之间的间隔
    /// </summary>
    private static readonly TimeSpan _servingPollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// 投递一次并给出了结方式
    /// </summary>
    /// <param name="dispatch">运行时交给代理的派发委托</param>
    /// <param name="delivery">这次投递，带尝试号与订阅方式</param>
    /// <param name="options">消息选项，取最多投递次数</param>
    /// <param name="app">应用，进入服务状态之前不派发</param>
    /// <param name="logger">代理自己的日志：派发抛出、进死信与广播丢弃记在它上面</param>
    /// <param name="cancellationToken">代理的停止信号（重放时是调用方的令牌），传给派发</param>
    /// <returns>了结方式与失败的错误；不抛出派发的异常</returns>
    public static ValueTask<MessageDeliveryOutcome> RunAsync(MessageDispatch dispatch, MessageDelivery delivery, MessagingOptions options, App app, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(logger);
        return RunCoreAsync(dispatch, delivery, options, app, logger, cancellationToken);
    }

    /// <summary>
    /// 一条投递用完了尝试次数时的了结方式
    /// </summary>
    /// <remarks>
    /// 竞争订阅进死信，广播订阅丢弃，各记一条警告。<see cref="RunAsync"/> 在最后一次尝试失败时调用它；代理在不经派发就要了结时直接调用：没法再重投（内存代理的队列已满），或接手的投递已经投过上限次数（Redis 回收来的条目）
    /// </remarks>
    /// <param name="delivery">用完尝试次数的投递，尝试号是最后一次的</param>
    /// <param name="error">最后一次失败的错误；不知道时为 <see langword="null"/>，日志里记 <c>?</c></param>
    /// <param name="logger">代理自己的日志</param>
    /// <returns><see cref="DeliveryDisposition.DeadLetter"/> 或 <see cref="DeliveryDisposition.Discard"/>，带着给出的错误</returns>
    public static MessageDeliveryOutcome Exhausted(MessageDelivery delivery, Error? error, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(logger);
        var errorCode = error?.Code ?? UnknownErrorCode;

        // 广播不进死信：死信是竞争订阅的共享留存，每个实例各自的一份失败没有可以重放的共享位置（RFC-0009「订阅方式」第 3 条）
        if (delivery.Mode == SubscriptionMode.Broadcast)
        {
            LogBroadcastDropped(logger, delivery.Envelope.Id, delivery.Topic, delivery.Attempt, errorCode);
            return new MessageDeliveryOutcome(DeliveryDisposition.Discard, error);
        }

        LogDeadLettered(logger, delivery.Envelope.Id, delivery.Topic, delivery.Attempt, errorCode);
        return new MessageDeliveryOutcome(DeliveryDisposition.DeadLetter, error);
    }

    /// <summary>
    /// 等服务状态、派发一次并归类
    /// </summary>
    private static async ValueTask<MessageDeliveryOutcome> RunCoreAsync(MessageDispatch dispatch, MessageDelivery delivery, MessagingOptions options, App app, ILogger logger, CancellationToken cancellationToken)
    {
        Result result;
        try
        {
            // 宿主未进入服务状态前不派发，避免处理器在特性尚未就绪时执行；排空中的应用同样等在这里，直到停止信号到来
            while (!app.State.IsServing())
            {
                await Task.Delay(_servingPollInterval, cancellationToken).ConfigureAwait(false);
            }

            result = await dispatch(delivery, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 等待或处理器被停止打断：不算一次尝试，default 即 Abandon
            return default;
        }
        catch (Exception exception)
        {
            // 抛出与返回失败一样算投递失败；令牌没取消时的取消异常（处理器自己的超时）也落在这里
            // 不记下来这个异常就没人看得到：经管道的处理器异常由管道记，到这里的是管道之外的（建作用域、应用不在服务状态）
            LogDispatchThrew(logger, delivery.Envelope.Id, delivery.Topic, delivery.Attempt, exception);
            result = Fault(exception);
        }

        if (result.IsSuccess)
        {
            return new MessageDeliveryOutcome(DeliveryDisposition.Complete, null);
        }

        // 停止期间的失败多半是取消造成的：管道把处理器的取消写成失败结果返回，不抛异常。照样不算一次尝试，
        // 否则停机会把正在处理的消息记成失败，赶上最后一次尝试还会把它送进死信
        if (cancellationToken.IsCancellationRequested)
        {
            return default;
        }

        return delivery.Attempt < options.MaxAttempts
            ? new MessageDeliveryOutcome(DeliveryDisposition.Retry, result.Error)
            : Exhausted(delivery, result.Error, logger);
    }

    /// <summary>
    /// 把派发抛出的异常转成投递失败的错误
    /// </summary>
    /// <remarks>
    /// 异常消息是处理器或驱动写的，带什么不由框架决定：<c>reason</c> 只写类型全名；框架异常带着框架自己定的错误码，写它的错误码并把它转成的错误留作内部错误（ADR-0115、ADR-0259）
    /// </remarks>
    private static Error Fault(Exception exception)
    {
        if (exception is FrameworkException framework)
        {
            return Error.Unexpected(MessagingErrors.HandlerFailed, MessagingMessages.HandlerFailed, MessageArguments.Of(("reason", framework.Code)), framework.ToError());
        }

        var type = exception.GetType();
        return Error.Unexpected(MessagingErrors.HandlerFailed, MessagingMessages.HandlerFailed, MessageArguments.Of(("reason", type.FullName ?? type.Name)));
    }

    /// <summary>
    /// 记录派发抛出了异常
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Dispatching message {MessageId} on topic {Topic} threw on attempt {Attempt}.")]
    private static partial void LogDispatchThrew(ILogger logger, string messageId, string topic, int attempt, Exception exception);

    /// <summary>
    /// 记录一条消息重试到上限后进入死信
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} on topic {Topic} was dead-lettered after attempt {Attempt} failed: {ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, string messageId, string topic, int attempt, string errorCode);

    /// <summary>
    /// 记录一条广播投递重试到上限后被丢弃
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Broadcast message {MessageId} on topic {Topic} was dropped after attempt {Attempt} failed: {ErrorCode}")]
    private static partial void LogBroadcastDropped(ILogger logger, string messageId, string topic, int attempt, string errorCode);
}
