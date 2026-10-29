// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Resilience;

/// <summary>
/// 简单熔断器。
/// </summary>
/// <remarks>
/// 连续失败达到阈值即打开，打开期间拒绝调用；到期进入半开放一次试探，试探成功关闭、失败重新打开。打开期间仍在途的调用结束时不改变状态（它们反映的是打开前的情况）。状态转移在锁内完成；时间来自 <see cref="TimeProvider"/>，便于测试推进。
/// </remarks>
internal sealed class CircuitBreaker
{
    /// <summary>
    /// 连续失败阈值。
    /// </summary>
    private readonly int _failureThreshold;

    /// <summary>
    /// 打开持续时长。
    /// </summary>
    private readonly TimeSpan _breakDuration;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 保护状态的锁。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 当前状态。
    /// </summary>
    private CircuitState _state = CircuitState.Closed;

    /// <summary>
    /// 连续失败次数（关闭态计数）。
    /// </summary>
    private int _failures;

    /// <summary>
    /// 打开态到期时刻。
    /// </summary>
    private DateTimeOffset _openUntil;

    /// <summary>
    /// 半开态是否已放出试探调用。
    /// </summary>
    private bool _probing;

    /// <summary>
    /// 创建。
    /// </summary>
    public CircuitBreaker(int failureThreshold, TimeSpan breakDuration, TimeProvider clock)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureThreshold, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(breakDuration, TimeSpan.Zero);
        _failureThreshold = failureThreshold;
        _breakDuration = breakDuration;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 当前状态。
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// 尝试进入。
    /// </summary>
    /// <remarks>
    /// 关闭态放行；打开态未到期拒绝并给出剩余时长，到期转半开放出一次试探；半开态已有试探在途时拒绝。
    /// </remarks>
    public bool TryEnter(out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            switch (_state)
            {
                case CircuitState.Closed:
                    retryAfter = TimeSpan.Zero;
                    return true;
                case CircuitState.Open when now < _openUntil:
                    retryAfter = _openUntil - now;
                    return false;
                case CircuitState.Open:
                    // 到期：半开，只放一次试探，其余仍拒绝，直到试探给出结论
                    _state = CircuitState.HalfOpen;
                    _probing = true;
                    retryAfter = TimeSpan.Zero;
                    return true;
                default:
                    if (_probing)
                    {
                        retryAfter = _breakDuration;
                        return false;
                    }

                    _probing = true;
                    retryAfter = TimeSpan.Zero;
                    return true;
            }
        }
    }

    /// <summary>
    /// 放弃一次已放行但没有执行的进入（随后被限流或并发拒绝、排队被取消）。
    /// </summary>
    /// <remarks>
    /// 半开态让出试探名额给下一个调用。
    /// </remarks>
    public void Abandon()
    {
        lock (_gate)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _probing = false;
            }
        }
    }

    /// <summary>
    /// 记一次成功。
    /// </summary>
    /// <remarks>
    /// 清零连续失败；半开态试探成功即关闭；打开态不变。
    /// </remarks>
    public void RecordSuccess()
    {
        lock (_gate)
        {
            _failures = 0;
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _probing = false;
            }
        }
    }

    /// <summary>
    /// 记一次失败。
    /// </summary>
    /// <remarks>
    /// 关闭态计数达到阈值即打开；半开态试探失败重新打开；打开态不变。
    /// </remarks>
    public void RecordFailure()
    {
        lock (_gate)
        {
            switch (_state)
            {
                case CircuitState.HalfOpen:
                    Open();
                    break;
                case CircuitState.Closed when ++_failures >= _failureThreshold:
                    Open();
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// 打开：记到期时刻、清零计数。
    /// </summary>
    private void Open()
    {
        _state = CircuitState.Open;
        _openUntil = _clock.GetUtcNow() + _breakDuration;
        _failures = 0;
        _probing = false;
    }
}
