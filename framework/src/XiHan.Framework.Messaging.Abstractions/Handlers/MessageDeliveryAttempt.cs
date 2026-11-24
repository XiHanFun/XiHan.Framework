// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
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
/// 派发被管道以 <see cref="KernelErrors.InvalidAppState"/> 拒绝（查看时应用在服务状态、调用进管道时已经不在）不是一次失败的尝试：处理器没有执行，等一个查看间隔后回到上面的等待，应用再进入服务状态时重新派发，停止信号到来即 <see cref="DeliveryDisposition.Abandon"/>。只认派发抛出的这个错误码，处理器自己返回的同码失败照常计入。状态说在服务、管道却一直拒绝时不无限地等：同一次尝试里第 10 次被拒绝按抛出的异常归成一次失败（ADR-0275）
/// </para>
/// <para>
/// 派发返回成功即 <see cref="DeliveryDisposition.Complete"/>。返回失败与抛出异常都算一次失败的尝试：抛出的异常转成 <see cref="MessagingErrors.HandlerFailed"/>，<c>reason</c> 只写异常类型的全名（<see cref="FrameworkException"/> 写它的错误码，原错误作内部错误），完整异常经 <c>logger</c> 记警告。失败之后尝试号小于 <see cref="MessagingOptions.MaxAttempts"/> 即 <see cref="DeliveryDisposition.Retry"/>，否则按 <see cref="Exhausted"/> 了结
/// </para>
/// <para>
/// 取消只认令牌：令牌已请求取消时抛出的 <see cref="OperationCanceledException"/>，以及令牌已请求取消时返回的失败或抛出的其他异常（管道把取消写成失败结果返回，不抛异常），都是 <see cref="DeliveryDisposition.Abandon"/>，不算一次失败。令牌没有取消时的 <see cref="OperationCanceledException"/>（处理器自己的超时）是一次失败
/// </para>
/// <para>
/// 不经派发的两种了结也在这里：用完了尝试次数（<see cref="Exhausted"/>）与收到的消息无法解码（<see cref="Undecodable"/>），都是竞争订阅进死信、广播订阅丢弃
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
    /// 同一次尝试里派发被管道以「应用不在服务状态」拒绝的次数上限
    /// </summary>
    /// <remarks>
    /// 第这么多次被拒绝时不再回去等，按抛出的异常归成一次失败的尝试：应用的状态只往前走，正常的拒绝一次尝试里至多一次，连续被拒绝说明状态与管道不一致
    /// </remarks>
    private const int MaxRejections = 10;

    /// <summary>
    /// 等应用进入服务状态时两次查看之间的间隔
    /// </summary>
    /// <remarks>
    /// 派发被拒绝之后再查看状态之前也等这么久
    /// </remarks>
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
    /// 收到一条无法解码的消息时的了结方式
    /// </summary>
    /// <remarks>
    /// 没有信封就没有可以交给处理器的投递，重试也解不开：不派发、不重试，也不等应用进入服务状态。竞争订阅进死信（代理把收到的字节原样写进死信），广播订阅丢弃，各记一条警告（RFC-0009「订阅方式」第 3 条、「流」第 2 条，ADR-0275）。与 <see cref="Exhausted"/> 成套：代理把这两个方法给出的 <see cref="DeliveryDisposition.DeadLetter"/> 与 <see cref="DeliveryDisposition.Discard"/> 翻译成同样的动作
    /// </remarks>
    /// <param name="subscription">消息来自的订阅：主题与订阅方式</param>
    /// <param name="error">解码给出的错误（<see cref="MessagingErrors.InvalidEnvelope"/>）；不知道时为 <see langword="null"/>，日志里记 <c>?</c></param>
    /// <param name="logger">代理自己的日志</param>
    /// <returns><see cref="DeliveryDisposition.DeadLetter"/> 或 <see cref="DeliveryDisposition.Discard"/>，带着给出的错误</returns>
    public static MessageDeliveryOutcome Undecodable(MessageSubscription subscription, Error? error, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(logger);
        // 没有消息 ID 可记，原因写解码错误的文案：它是框架自己的文字，说明是哪一项不合法
        var reason = error?.Message ?? UnknownErrorCode;

        // 广播不进死信：与用完尝试次数时一样，每个实例各自的一份坏消息没有可以重放的共享位置
        if (subscription.Mode == SubscriptionMode.Broadcast)
        {
            LogBroadcastUndecodable(logger, subscription.Topic, reason);
            return new MessageDeliveryOutcome(DeliveryDisposition.Discard, error);
        }

        LogUndecodable(logger, subscription.Topic, reason);
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
            for (var rejections = 0; ;)
            {
                // 宿主未进入服务状态前不派发，避免处理器在特性尚未就绪时执行；排空中的应用同样等在这里，直到停止信号到来
                while (!app.State.IsServing())
                {
                    await Task.Delay(_servingPollInterval, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    result = await dispatch(delivery, cancellationToken).ConfigureAwait(false);
                    break;
                }
                catch (FrameworkException exception) when (string.Equals(exception.Code, KernelErrors.InvalidAppState, StringComparison.Ordinal))
                {
                    // 查看时应用在服务状态、调用进管道时已经不在：管道在入口拒绝，处理器没有执行，这不是一次失败的尝试
                    // 出口：状态说在服务、管道却一直拒绝（派发绑定的不是这个应用）时不能无限地等，第 MaxRejections 次按抛出的异常归类
                    if (++rejections >= MaxRejections)
                    {
                        throw;
                    }

                    LogRejected(logger, delivery.Envelope.Id, delivery.Topic, delivery.Attempt);
                }

                // 被拒绝之后先等一个查看间隔再回去看状态：状态与管道不一致时两次派发之间也有间隔，不空转；
                // 状态已经翻成不在服务的，接着等在上面的循环里，直到再进入服务状态或停止信号到来
                await Task.Delay(_servingPollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 等待或处理器被停止打断：不算一次失败，default 即 Abandon
            return default;
        }
        catch (Exception exception)
        {
            // 抛出与返回失败一样算投递失败；令牌没取消时的取消异常（处理器自己的超时）也落在这里
            // 不记下来这个异常就没人看得到：经管道的处理器异常由管道记，到这里的是管道之外的（建作用域失败、一直被管道拒绝）
            LogDispatchThrew(logger, delivery.Envelope.Id, delivery.Topic, delivery.Attempt, exception);
            result = Fault(exception);
        }

        if (result.IsSuccess)
        {
            return new MessageDeliveryOutcome(DeliveryDisposition.Complete, null);
        }

        // 停止期间的失败多半是取消造成的：管道把处理器的取消写成失败结果返回，不抛异常。照样不算一次失败，
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
    /// 记录派发被管道以应用不在服务状态拒绝
    /// </summary>
    /// <remarks>
    /// Debug 级别：应用开始排空的那一刻正在派发的投递会遇到，是预期的状态，不是故障
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Dispatching message {MessageId} on topic {Topic} was rejected because the application is not serving; attempt {Attempt} is not counted.")]
    private static partial void LogRejected(ILogger logger, string messageId, string topic, int attempt);

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

    /// <summary>
    /// 记录一条无法解码的消息进入死信
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Topic {Topic} received an undecodable message; it was dead-lettered: {Reason}")]
    private static partial void LogUndecodable(ILogger logger, string topic, string reason);

    /// <summary>
    /// 记录一条无法解码的广播消息被丢弃
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Topic {Topic} delivered an undecodable broadcast message; it was dropped: {Reason}")]
    private static partial void LogBroadcastUndecodable(ILogger logger, string topic, string reason);
}
