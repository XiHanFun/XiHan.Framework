// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using XiHan.Framework.Plugins.Protocol.V1;

namespace EchoPlugin;

/// <summary>
/// 插件进程内的状态：生命周期、配置、在途调用、收到的事件与健康订阅
/// </summary>
/// <remarks>
/// 状态迁移按 <c>spec/plugin/lifecycle.md</c> §7：每个生命周期 RPC 只在它接受的状态下生效，其余组合由调用方以 <c>FailedPrecondition</c> 拒绝；只有 Ready 与 Degraded 可服务，在途调用在可服务时登记，停止与收尾据此排空
/// </remarks>
internal sealed class PluginState : IDisposable
{
    /// <summary>
    /// 保护状态、在途计数与停止标记的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 收到的事件 ID（按投递顺序）
    /// </summary>
    private readonly ConcurrentQueue<string> _events = new();

    /// <summary>
    /// 收到的投递，记为「方式:事件 ID」（按投递顺序）
    /// </summary>
    private readonly ConcurrentQueue<string> _deliveries = new();

    /// <summary>
    /// 健康状态变化的订阅者
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Func<Task>> _watchers = new();

    /// <summary>
    /// 停止或收尾时触发，在途调用经它收到取消
    /// </summary>
    private readonly CancellationTokenSource _calls = new();

    /// <summary>
    /// 当前状态
    /// </summary>
    private LifecycleState _state = LifecycleState.Declared;

    /// <summary>
    /// 启动回调是否成功过
    /// </summary>
    private bool _started;

    /// <summary>
    /// 停止回调是否已调过
    /// </summary>
    /// <remarks>
    /// 进程里最多调一次（lifecycle.md §7.5）
    /// </remarks>
    private bool _stopCalled;

    /// <summary>
    /// 是否已经接受过 Stop
    /// </summary>
    /// <remarks>
    /// 停止中再 Stop 以 FailedPrecondition 拒绝：没启动过的插件接受 Stop 之后状态不迁移，只靠这个标记
    /// </remarks>
    private bool _stopRequested;

    /// <summary>
    /// 在途的调用与投递数
    /// </summary>
    private int _inflight;

    /// <summary>
    /// 在途数归零时完成
    /// </summary>
    /// <remarks>
    /// 排空开始时建立，没有排空时为空
    /// </remarks>
    private TaskCompletionSource? _drained;

    /// <summary>
    /// 清单文件的 JSON 文本（与宿主核对）
    /// </summary>
    /// <remarks>
    /// 握手写清单文件的原文，开头的 UTF-8 BOM 不算原文（handshake.md §3）：<see cref="File.ReadAllText(string)"/> 按 BOM 识别编码并去掉它
    /// </remarks>
    public string ManifestJson { get; } = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "xihan.manifest.json"));

    /// <summary>
    /// 宿主下发的配置节（<c>XiHan:Echo</c>）
    /// </summary>
    public JsonObject Configuration { get; private set; } = [];

    /// <summary>
    /// 宿主授予的权限
    /// </summary>
    public IReadOnlyList<string> GrantedPermissions { get; private set; } = [];

    /// <summary>
    /// 当前生命周期状态
    /// </summary>
    public LifecycleState State
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
    /// 启动回调是否成功过
    /// </summary>
    public bool Started
    {
        get
        {
            lock (_gate)
            {
                return _started;
            }
        }
    }

    /// <summary>
    /// 收到的事件 ID 快照
    /// </summary>
    public IReadOnlyList<string> Events => _events.ToArray();

    /// <summary>
    /// 收到的投递快照，每项是「方式:事件 ID」
    /// </summary>
    public IReadOnlyList<string> Deliveries => _deliveries.ToArray();

    /// <summary>
    /// 配置里的问候语
    /// </summary>
    /// <remarks>
    /// 没有配置时为空；成员名按 ASCII 不区分大小写匹配，与 <c>IConfiguration</c> 一致（lifecycle.md §7.2）
    /// </remarks>
    public string? Greeting => Configuration.FirstOrDefault(static m => string.Equals(m.Key, nameof(Greeting), StringComparison.OrdinalIgnoreCase)).Value?.GetValue<string>();

    /// <summary>
    /// 停止或收尾时触发的取消令牌
    /// </summary>
    public CancellationToken Stopping => _calls.Token;

    /// <summary>
    /// 状态为 <paramref name="expected"/> 时迁移到 <paramref name="next"/>
    /// </summary>
    /// <remarks>
    /// 不是 <paramref name="expected"/> 时什么都不做、返回假，调用方以 <c>FailedPrecondition</c> 拒绝
    /// </remarks>
    public bool TryAdvance(LifecycleState expected, LifecycleState next)
    {
        lock (_gate)
        {
            if (_state != expected)
            {
                return false;
            }

            _state = next;
        }

        Notify();
        return true;
    }

    /// <summary>
    /// 采纳 Configure，或在配置节不合法时进入 Failed
    /// </summary>
    /// <remarks>
    /// 只在 Resolved 时生效；返回是否迁移过状态（假即次序不对）
    /// </remarks>
    public bool TryConfigure(JsonObject? configuration, IEnumerable<string> granted)
    {
        lock (_gate)
        {
            if (_state != LifecycleState.Resolved)
            {
                return false;
            }

            if (configuration is not null)
            {
                Configuration = configuration;
                GrantedPermissions = granted.ToList();
            }

            _state = configuration is null ? LifecycleState.Failed : LifecycleState.Configured;
        }

        Notify();
        return true;
    }

    /// <summary>
    /// 启动回调成功：Started，之后按一次健康的结果进入 Ready、Degraded 或 Failed
    /// </summary>
    /// <remarks>
    /// 只在 Configured 时生效，返回假即次序不对；应答 <c>StartResponse</c> 时状态已经是健康的结果（lifecycle.md §7.3）
    /// </remarks>
    public bool TryStart(Func<LifecycleState> health)
    {
        lock (_gate)
        {
            if (_state != LifecycleState.Configured)
            {
                return false;
            }

            _state = LifecycleState.Started;
            _started = true;
        }

        var next = health();
        lock (_gate)
        {
            if (_state == LifecycleState.Started)
            {
                _state = next;
            }
        }

        Notify();
        return true;
    }

    /// <summary>
    /// 可服务期间按健康的结果迁移状态
    /// </summary>
    /// <remarks>
    /// 只在 Ready 与 Degraded 之间与到 Failed 迁移，Failed 之后不再回来（lifecycle.md §7.4、lifecycle-transitions.json）；其他时候什么都不做
    /// </remarks>
    public void ApplyHealth(LifecycleState next)
    {
        bool changed;
        lock (_gate)
        {
            changed = _state is LifecycleState.Ready or LifecycleState.Degraded && _state != next;
            if (changed)
            {
                _state = next;
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// 登记一次在途调用或投递
    /// </summary>
    /// <remarks>
    /// 不在可服务状态时不登记，返回此刻的状态供错误使用；判定与登记在同一把锁里，停止开始排空之后不会再有新的在途调用
    /// </remarks>
    public bool TryEnter(out LifecycleState state)
    {
        lock (_gate)
        {
            state = _state;
            if (_state is not (LifecycleState.Ready or LifecycleState.Degraded))
            {
                return false;
            }

            _inflight++;
            return true;
        }
    }

    /// <summary>
    /// 撤销一次在途登记
    /// </summary>
    public void Exit()
    {
        lock (_gate)
        {
            // 排空中归零即通知等待方：停止与收尾不再等
            if (--_inflight == 0)
            {
                _drained?.TrySetResult();
            }
        }
    }

    /// <summary>
    /// 能否接受 Stop
    /// </summary>
    /// <remarks>
    /// Declared、Draining、Stopped 之外都接受（lifecycle.md §7.1）；接受时可服务的状态迁到 Draining
    /// </remarks>
    public bool TryBeginStop(out bool started)
    {
        lock (_gate)
        {
            started = _started;
            if (_stopRequested || _state is LifecycleState.Declared or LifecycleState.Draining or LifecycleState.Stopped)
            {
                return false;
            }

            _stopRequested = true;
            BeginDraining();
        }

        Notify();
        return true;
    }

    /// <summary>
    /// 收尾请求：stdin 读到结尾或收到 SIGTERM
    /// </summary>
    /// <remarks>
    /// 可服务时进入 Draining（其他状态不迁移），立即取消全部在途调用（lifecycle.md §7.7）
    /// </remarks>
    public void BeginShutdown()
    {
        lock (_gate)
        {
            BeginDraining();
        }

        _calls.Cancel();
        Notify();
    }

    /// <summary>
    /// 等在途调用结束，最长到 <paramref name="grace"/>，到时取消剩下的
    /// </summary>
    public async Task DrainAsync(TimeSpan grace)
    {
        Task drained;
        lock (_gate)
        {
            _drained ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_inflight == 0)
            {
                _drained.TrySetResult();
            }

            drained = _drained.Task;
        }

        if (await Task.WhenAny(drained, Task.Delay(grace)).ConfigureAwait(false) != drained)
        {
            // 宽限到了：只经令牌通知仍在执行的调用，不强行中止、也不再等
            await _calls.CancelAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 停止回调要不要调
    /// </summary>
    /// <remarks>
    /// 启动回调成功过、且还没调过时返回真并记下已调，进程里最多一次
    /// </remarks>
    public bool ClaimStop()
    {
        lock (_gate)
        {
            if (!_started || _stopCalled)
            {
                return false;
            }

            _stopCalled = true;
            return true;
        }
    }

    /// <summary>
    /// 停止回调完成：Draining 进入 Stopped
    /// </summary>
    /// <remarks>
    /// 启动之后报过 Failed 的不迁移（lifecycle.md §7.5）
    /// </remarks>
    public void CompleteStop()
    {
        lock (_gate)
        {
            if (_state == LifecycleState.Draining)
            {
                _state = LifecycleState.Stopped;
            }
        }

        Notify();
    }

    /// <summary>
    /// 记一条收到的事件与它投递的订阅方式
    /// </summary>
    public void Record(string eventId, string mode)
    {
        _events.Enqueue(eventId);
        _deliveries.Enqueue($"{mode}:{eventId}");
    }

    /// <summary>
    /// 订阅状态变化
    /// </summary>
    /// <remarks>
    /// 返回取消订阅的句柄
    /// </remarks>
    public IDisposable Watch(Func<Task> onChange)
    {
        var id = Guid.NewGuid();
        _watchers[id] = onChange;
        return new Subscription(() => _watchers.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public void Dispose() => _calls.Dispose();

    /// <summary>
    /// 可服务的状态迁到 Draining
    /// </summary>
    /// <remarks>
    /// 调用方持有 <see cref="_gate"/>
    /// </remarks>
    private void BeginDraining()
    {
        if (_state is LifecycleState.Ready or LifecycleState.Degraded or LifecycleState.Started)
        {
            _state = LifecycleState.Draining;
        }
    }

    /// <summary>
    /// 通知健康订阅者
    /// </summary>
    /// <remarks>
    /// 在锁外调用：订阅者会回头读状态
    /// </remarks>
    private void Notify()
    {
        foreach (var watcher in _watchers.Values)
        {
            _ = watcher();
        }
    }

    /// <summary>
    /// 取消订阅的句柄
    /// </summary>
    private sealed class Subscription(Action dispose) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => dispose();
    }
}
