// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// 一次 Provider 调用的超时令牌与归因。
/// </summary>
/// <remarks>
/// <see cref="AIProviderOptions.Timeout"/> 按注入的 <see cref="TimeProvider"/> 落成定时取消，与调用方令牌链接后交给 Provider（<see cref="Token"/>）。
/// 归因只看令牌，Provider 抛出与写成结果同一规则（RFC-0022「超时与取消」，ADR-0095）：超时令牌已触发即超时，不论调用方令牌是否也已触发；
/// 超时令牌没有触发、调用方令牌已触发即取消；两个都没有触发时 Provider 抛出的取消是它自己的传输超时。
/// </remarks>
internal sealed class ProviderTimeout : IDisposable
{
    /// <summary>
    /// 时限。
    /// </summary>
    private readonly TimeSpan _timeout;

    /// <summary>
    /// 调用方令牌。
    /// </summary>
    private readonly CancellationToken _caller;

    /// <summary>
    /// 超时计时器。
    /// </summary>
    /// <remarks>
    /// 按注入的时间源创建：测试推进虚拟时间即触发。
    /// </remarks>
    private readonly CancellationTokenSource _timer;

    /// <summary>
    /// 调用方令牌与超时令牌的链接。
    /// </summary>
    private readonly CancellationTokenSource _linked;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="timeout">Provider 的时限。</param>
    /// <param name="timeProvider">时间源。</param>
    /// <param name="cancellationToken">调用方令牌。</param>
    /// <param name="armed">是否立即开始计时；流式按每次等待计时，创建时不计时。</param>
    public ProviderTimeout(TimeSpan timeout, TimeProvider timeProvider, CancellationToken cancellationToken, bool armed = true)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeout = timeout;
        _caller = cancellationToken;
        _timer = new CancellationTokenSource(armed ? timeout : Timeout.InfiniteTimeSpan, timeProvider);
        _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timer.Token);
    }

    /// <summary>
    /// 交给 Provider 的令牌。
    /// </summary>
    public CancellationToken Token => _linked.Token;

    /// <summary>
    /// 超时令牌是否已触发。
    /// </summary>
    public bool Elapsed => _timer.IsCancellationRequested;

    /// <summary>
    /// 从现在起重新计时。
    /// </summary>
    /// <remarks>
    /// 已触发的计时器不再复位：超时一旦发生就是本次调用的结论。
    /// </remarks>
    public void Arm() => _timer.CancelAfter(_timeout);

    /// <summary>
    /// 停止计时。
    /// </summary>
    public void Disarm() => _timer.CancelAfter(Timeout.InfiniteTimeSpan);

    /// <summary>
    /// Provider 抛出的异常是否按超时报告。
    /// </summary>
    /// <remarks>
    /// 返回 false 的取消是调用方的取消，原样抛出；其他异常由调用处按不可达报告。
    /// </remarks>
    public bool IsTimeout(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is TimeoutException)
        {
            return true;
        }

        if (exception is not OperationCanceledException)
        {
            return false;
        }

        // 超时令牌已触发即超时，不论 Provider 报的是哪个令牌：两个都触发时 OpenAI SDK 先报调用方的取消，
        // 按它的异常归因就会把同一个超时随调度报成取消
        if (Elapsed)
        {
            return true;
        }

        // 只有调用方取消：是取消，原样交给调用方。两个都没触发：Provider 自己的传输超时（如 NetworkTimeout 早于本计时器到点）
        return !_caller.IsCancellationRequested;
    }

    /// <summary>
    /// Provider 正常返回之后，按令牌决定结果是否有效。
    /// </summary>
    /// <remarks>
    /// 返回 true 表示按超时报告；调用方令牌已触发时抛出 <see cref="OperationCanceledException"/>。
    /// </remarks>
    public bool ReturnedAfterTimeout()
    {
        // 写成结果：Provider 可能接住了取消、返回截断的应答或条数不全的嵌入，只看结果分不出来，按令牌归因——
        // 超时令牌已触发即超时；只有调用方取消时同样不把结果交出去，与抛出路径同一个回答
        if (Elapsed)
        {
            return true;
        }

        _caller.ThrowIfCancellationRequested();
        return false;
    }

    /// <summary>
    /// 流里报告超时的异常。
    /// </summary>
    /// <param name="provider">Provider 名。</param>
    /// <param name="inner">Provider 抛出的异常；写成结果时为空。</param>
    public TimeoutException Exceeded(string provider, Exception? inner)
        => new($"The AI provider '{provider}' did not respond within its timeout of {_timeout}.", inner);

    /// <inheritdoc />
    public void Dispose()
    {
        _linked.Dispose();
        _timer.Dispose();
    }
}
