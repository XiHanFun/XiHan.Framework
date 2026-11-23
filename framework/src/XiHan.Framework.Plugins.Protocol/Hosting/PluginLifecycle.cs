// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 一个插件在宿主一侧的生命周期：当前状态与错误，以及失败之后的退避重启
/// </summary>
/// <remarks>
/// 进程外与 Wasm 运行时各持有一个（<c>spec/plugin/lifecycle.md</c> §1、§6，ADR-0260）。状态转移由运行时裁决后经 <see cref="Enter"/> 记下；重启的节奏在这里：1 秒起翻倍、上限 60 秒，时间窗内超过次数上限后保持 Failed。「重新起来」做什么由载体经 <see cref="IPluginCarrier"/> 给出，载体自己的串行化（生命周期锁、调用锁）不在这里
/// </remarks>
internal sealed class PluginLifecycle
{
    /// <summary>
    /// 重启退避上限
    /// </summary>
    private static readonly TimeSpan _maxBackoff = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 插件清单
    /// </summary>
    private readonly Manifest _manifest;

    /// <summary>
    /// 承载插件的载体
    /// </summary>
    private readonly IPluginCarrier _carrier;

    /// <summary>
    /// 时间窗内最多自动重启多少次
    /// </summary>
    private readonly int _restartLimit;

    /// <summary>
    /// 重启计数的时间窗
    /// </summary>
    private readonly TimeSpan _restartWindow;

    /// <summary>
    /// 取当前时钟
    /// </summary>
    /// <remarks>
    /// 宿主附着到容器后会换钟，每次用时现取
    /// </remarks>
    private readonly Func<TimeProvider> _clock;

    /// <summary>
    /// 显式停止后取消：终止退避中的重启
    /// </summary>
    private readonly CancellationToken _lifetime;

    /// <summary>
    /// 保护状态、错误与重启记录的锁
    /// </summary>
    private readonly Lock _stateLock = new();

    /// <summary>
    /// 时间窗内的重启时刻
    /// </summary>
    private readonly Queue<DateTimeOffset> _restartTimes = new();

    /// <summary>
    /// 当前状态
    /// </summary>
    private LifecycleState _state = LifecycleState.Declared;

    /// <summary>
    /// 当前错误
    /// </summary>
    private Error? _error;

    /// <summary>
    /// 累计自动重启次数
    /// </summary>
    private int _restarts;

    /// <summary>
    /// 有重启在退避中
    /// </summary>
    private volatile bool _restartPending;

    /// <summary>
    /// 创建生命周期，初始状态为 Declared
    /// </summary>
    /// <param name="manifest">插件清单</param>
    /// <param name="carrier">承载插件的载体</param>
    /// <param name="restartLimit">时间窗内最多自动重启多少次</param>
    /// <param name="restartWindow">重启计数的时间窗</param>
    /// <param name="clock">取当前时钟</param>
    /// <param name="lifetime">显式停止时取消的令牌</param>
    public PluginLifecycle(Manifest manifest, IPluginCarrier carrier, int restartLimit, TimeSpan restartWindow, Func<TimeProvider> clock, CancellationToken lifetime)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _carrier = carrier ?? throw new ArgumentNullException(nameof(carrier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _restartLimit = restartLimit;
        _restartWindow = restartWindow;
        _lifetime = lifetime;
    }

    /// <summary>
    /// 当前状态
    /// </summary>
    public LifecycleState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// 当前错误
    /// </summary>
    public Error? Error
    {
        get
        {
            lock (_stateLock)
            {
                return _error;
            }
        }
    }

    /// <summary>
    /// 对外的状态快照
    /// </summary>
    /// <remarks>
    /// 状态与错误在同一把锁里读出，不会一新一旧
    /// </remarks>
    public PluginStatus Status
    {
        get
        {
            lock (_stateLock)
            {
                return new PluginStatus(_manifest, _state, _error);
            }
        }
    }

    /// <summary>
    /// 累计自动重启次数
    /// </summary>
    public int Restarts => Volatile.Read(ref _restarts);

    /// <summary>
    /// 有重启在退避中
    /// </summary>
    /// <remarks>
    /// 期间的调用直接按不可用失败，不绕过退避
    /// </remarks>
    public bool IsRestartPending => _restartPending;

    /// <summary>
    /// 进入某状态
    /// </summary>
    /// <param name="state">进入的状态</param>
    /// <param name="error">随状态记下的错误；没有时为空</param>
    public void Enter(LifecycleState state, Error? error)
    {
        lock (_stateLock)
        {
            _state = state;
            _error = error;
        }

        // 日志在锁外记：载体的日志器可能很慢，读状态的调用不该等它
        _carrier.OnStateEntered(state);
    }

    /// <summary>
    /// 进入 Failed
    /// </summary>
    /// <param name="error">失败的原因</param>
    public void Fail(Error error) => Enter(LifecycleState.Failed, error);

    /// <summary>
    /// 插件不可用的失败，带着此刻的状态
    /// </summary>
    public Error Unavailable()
        => Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", _manifest.Id), ("state", State.ToString())));

    /// <summary>
    /// 记下有重启要排
    /// </summary>
    /// <remarks>
    /// 崩溃处理在载体的生命周期锁内先调它、出了锁再 <see cref="RestartAsync"/>：等锁的调用拿到锁时已经看得到，不会抢在退避前把插件带起来
    /// </remarks>
    public void MarkRestartPending() => _restartPending = true;

    /// <summary>
    /// 启动阶段失败后在后台按退避重试
    /// </summary>
    /// <remarks>
    /// 载体已停止或已有重启在退避中时什么都不做
    /// </remarks>
    public void ScheduleRestart()
    {
        if (_carrier.IsStopped || _restartPending)
        {
            return;
        }

        _restartPending = true;
        _ = RestartAsync();
    }

    /// <summary>
    /// 退避重启，直到成功、次数用完或载体停止
    /// </summary>
    /// <remarks>
    /// 每一轮：清掉时间窗外的记录、按窗内次数算退避（1 秒起翻倍、上限 60 秒）、等待、记一次重启、交载体重试。窗内次数达到上限即停下，插件保持 Failed，直到被卸载再加载
    /// </remarks>
    public async Task RestartAsync()
    {
        while (!_lifetime.IsCancellationRequested)
        {
            int attempt;
            lock (_stateLock)
            {
                // 只数时间窗内的重启：插件稳定运行满一个时间窗，退避自然归零
                var now = _clock().GetUtcNow();
                while (_restartTimes.TryPeek(out var oldest) && now - oldest > _restartWindow)
                {
                    _restartTimes.Dequeue();
                }

                attempt = _restartTimes.Count;
            }

            if (attempt >= _restartLimit)
            {
                // 次数用完：放开退避标志，之后的调用可以按需再试一次启动，后台不再重试
                _restartPending = false;
                _carrier.OnRestartLimitReached(_restartLimit, _restartWindow);
                return;
            }

            var delay = TimeSpan.FromSeconds(Math.Min(_maxBackoff.TotalSeconds, Math.Pow(2, attempt)));
            _carrier.OnRestartScheduled(delay, attempt + 1);
            try
            {
                await Task.Delay(delay, _clock(), _lifetime).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            lock (_stateLock)
            {
                _restartTimes.Enqueue(_clock().GetUtcNow());
                _restarts++;
            }

            // 退避结束才放开标志：重试期间到达的调用与这次重试排同一把生命周期锁，不再被挡在外面
            _restartPending = false;
            Result restarted;
            try
            {
                restarted = await _carrier.RecoverAsync(_lifetime).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (restarted.IsSuccess)
            {
                _carrier.OnRestarted(Restarts);
                return;
            }

            if (_carrier.IsStopped)
            {
                // 期间被显式停止，或声明登记不进去而按发现时被拒处理：不再重试
                return;
            }

            // 这次没起来：先挡住调用再进下一轮退避
            _restartPending = true;
        }
    }
}
