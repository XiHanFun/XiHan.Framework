// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Error = XiHan.Framework.Primitives.Error;
using LifecycleState = XiHan.Framework.Primitives.LifecycleState;
using ProtoBinding = XiHan.Framework.Plugins.Protocol.V1.Binding;
using ProtoLifecycleState = XiHan.Framework.Plugins.Protocol.V1.LifecycleState;
using Status = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 一个进程外插件的运行时：进程的启动、握手、配置、启动、健康监视、崩溃重启与停止（<c>spec/plugin/lifecycle.md</c>），以及经它的调用与事件投递。
/// </summary>
internal sealed partial class PluginRuntime : IDisposable
{
    /// <summary>
    /// 重启退避上限。
    /// </summary>
    private static readonly TimeSpan _maxBackoff = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 宿主环境。
    /// </summary>
    private readonly PluginHostContext _host;

    /// <summary>
    /// 串行化启动、停止与重启的互斥量。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 保护状态字段的锁。
    /// </summary>
    private readonly Lock _stateLock = new();

    /// <summary>
    /// 时间窗内的重启时刻。
    /// </summary>
    private readonly Queue<DateTimeOffset> _restartTimes = new();

    /// <summary>
    /// 显式停止后取消：终止退避中的重启。
    /// </summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>
    /// 当前状态。
    /// </summary>
    private LifecycleState _state = LifecycleState.Declared;

    /// <summary>
    /// 当前错误。
    /// </summary>
    private Error? _error;

    /// <summary>
    /// 当前进程；没有运行中的进程为空。
    /// </summary>
    private PluginProcess? _process;

    /// <summary>
    /// 当前进程的监视（健康与退出）；停止时取消，退出监视据此区分有序停止与崩溃。
    /// </summary>
    private CancellationTokenSource? _monitor;

    /// <summary>
    /// 首次成功握手的结果；重启后核对。
    /// </summary>
    private HandshakeInfo? _handshake;

    /// <summary>
    /// 累计自动重启次数。
    /// </summary>
    private int _restarts;

    /// <summary>
    /// 实例序号，拼进实例 ID。
    /// </summary>
    private int _instance;

    /// <summary>
    /// 已显式停止，不再启动也不再重启。
    /// </summary>
    private volatile bool _stopping;

    /// <summary>
    /// 已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 崩溃后有重启在退避中；期间的调用直接按不可用失败，不绕过退避。
    /// </summary>
    private volatile bool _restartPending;

    /// <summary>
    /// 创建运行时。
    /// </summary>
    public PluginRuntime(Manifest manifest, string directory, string executable, PermissionGrant permissions, PluginHostContext host, ILogger logger)
    {
        Manifest = manifest;
        Directory = directory;
        Executable = executable;
        Permissions = permissions;
        _host = host;
        Logger = logger;
    }

    /// <summary>
    /// 清单。
    /// </summary>
    public Manifest Manifest { get; }

    /// <summary>
    /// 插件目录。
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// 可执行文件绝对路径。
    /// </summary>
    public string Executable { get; }

    /// <summary>
    /// 授予的权限。
    /// </summary>
    public PermissionGrant Permissions { get; }

    /// <summary>
    /// 日志；宿主附着到容器后换成真正的日志工厂产出的实例。
    /// </summary>
    public ILogger Logger { get; set; }

    /// <summary>
    /// 是否懒激活：清单 <c>activation</c> 不含 <c>onStartup</c>。
    /// </summary>
    public bool IsLazy => !Manifest.Activation.Contains("onStartup", StringComparer.Ordinal);

    /// <summary>
    /// 首次成功握手的结果；尚未握手成功为空。
    /// </summary>
    public HandshakeInfo? Handshake => _handshake;

    /// <summary>
    /// 当前状态。
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
    /// 当前错误。
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
    /// 对外的状态快照。
    /// </summary>
    public PluginStatus Status
    {
        get
        {
            lock (_stateLock)
            {
                return new PluginStatus(Manifest, _state, _error);
            }
        }
    }

    /// <summary>
    /// 进程在运行且处于可服务状态。
    /// </summary>
    public bool IsServing => _process is { HasExited: false } && State is LifecycleState.Started or LifecycleState.Ready or LifecycleState.Degraded;

    /// <summary>
    /// 诊断快照。
    /// </summary>
    public OutOfProcessPluginStatus Describe()
        => new(Status, _process is { HasExited: false } process ? process.ProcessId : null, _handshake?.ProtocolVersion, _restarts, IsLazy);

    /// <summary>
    /// 是否订阅了该主题与类型。
    /// </summary>
    public bool IsSubscribedTo(string topic, string messageType)
        => _handshake?.Subscriptions.Any(s => s.Matches(topic, messageType)) == true;

    /// <summary>
    /// 探测启动：启动进程并握手；懒激活的插件随即停止进程，其余保持运行等待 <see cref="StartAsync"/>。
    /// </summary>
    public async Task<Result<HandshakeInfo>> DiscoverAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var launched = await LaunchAndHandshakeAsync(cancellationToken).ConfigureAwait(false);
            if (!launched.TryGetValue(out var info))
            {
                return launched.Error!;
            }

            if (IsLazy)
            {
                // 懒激活只为取得声明：拿到就停，首次调用或事件到达时再启动
                await ShutdownProcessAsync("unload", LifecycleState.Resolved, cancellationToken).ConfigureAwait(false);
            }

            return info;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 启动（<c>onStartup</c> 的插件）；懒激活的插件什么都不做。
    /// </summary>
    public Task<Result> StartAsync(CancellationToken cancellationToken)
        => IsLazy ? Task.FromResult(Result.Success()) : EnsureRunningAsync(cancellationToken);

    /// <summary>
    /// 显式停止：不再重启，发 <c>Stop</c> 并在宽限内等待退出，超时终止。
    /// </summary>
    public async Task StopAsync(string reason, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        _stopping = true;
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ShutdownProcessAsync(reason, LifecycleState.Stopped, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 调用插件的一个操作。
    /// </summary>
    public async ValueTask<Result<object>> InvokeAsync(string operation, ReadOnlyMemory<byte> payload, InvocationContext context)
    {
        var acquired = await AcquireAsync(context.CancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var process))
        {
            return acquired.Error!;
        }

        var request = new InvokeRequest
        {
            Operation = operation,
            Payload = ByteString.CopyFrom(payload.Span),
            Metadata = PluginMetadata.FromContext(context),
        };
        try
        {
            var response = await process.Client.InvokeAsync(request, PluginMetadata.CallOptionsFor(context)).ConfigureAwait(false);
            if (response.Status is { Ok: true })
            {
                return Result.Success<object>(new PluginPayload(response.Payload.Memory));
            }

            return PluginErrorMapper.FromStatus(response.Status, Manifest.Id, operation).Error!;
        }
        catch (RpcException exception)
        {
            return PluginErrorMapper.FromRpc(exception, Manifest.Id, operation, State, context.IsExpired(_host.Clock.GetUtcNow()));
        }
    }

    /// <summary>
    /// 把一条消息投递给插件。
    /// </summary>
    public async ValueTask<Result> DeliverAsync(MessageDelivery delivery, InvocationContext context)
    {
        var acquired = await AcquireAsync(context.CancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var process))
        {
            return acquired.Error!;
        }

        var request = new HandleEventRequest
        {
            Envelope = PluginEnvelopeMapper.ToProto(delivery.Topic, delivery.Envelope),
            Metadata = PluginMetadata.FromContext(context),
            Attempt = (uint)delivery.Attempt,
        };
        try
        {
            var response = await process.Client.HandleEventAsync(request, PluginMetadata.CallOptionsFor(context)).ConfigureAwait(false);
            return PluginErrorMapper.FromStatus(response.Status, Manifest.Id, "event");
        }
        catch (RpcException exception)
        {
            return PluginErrorMapper.FromRpc(exception, Manifest.Id, "event", State, context.IsExpired(_host.Clock.GetUtcNow()));
        }
    }

    /// <summary>
    /// 释放同步原语；调用前应已 <see cref="StopAsync"/>。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _monitor?.Dispose();
        _lifetime.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// 取可服务的进程：没有在运行就按需启动（懒激活、停止后的首次调用）；退避中的重启不被绕过。
    /// </summary>
    private async Task<Result<PluginProcess>> AcquireAsync(CancellationToken cancellationToken)
    {
        if (IsServing)
        {
            return _process!;
        }

        if (_stopping || _restartPending)
        {
            return Unavailable();
        }

        var running = await EnsureRunningAsync(cancellationToken).ConfigureAwait(false);
        if (running.IsFailure)
        {
            return running.Error!;
        }

        return IsServing ? _process! : Unavailable();
    }

    /// <summary>
    /// 保证进程在运行并已 <c>Start</c>：必要时启动、握手、配置、启动并开始监视。
    /// </summary>
    private async Task<Result> EnsureRunningAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_stopping)
            {
                return Unavailable();
            }

            if (IsServing)
            {
                return Result.Success();
            }

            if (_process is null || _process.HasExited)
            {
                if (_process is { } stale)
                {
                    await DiscardAsync(stale).ConfigureAwait(false);
                }

                var launched = await LaunchAndHandshakeAsync(cancellationToken).ConfigureAwait(false);
                if (launched.IsFailure)
                {
                    return launched.Error!;
                }
            }

            var process = _process!;
            var configured = await StepAsync(process, "configure", () => process.Client.ConfigureAsync(BuildConfigure(), deadline: DateTime.UtcNow + _host.Options.StartTimeout, cancellationToken: cancellationToken).ResponseAsync, static r => r.Status).ConfigureAwait(false);
            if (configured.IsFailure)
            {
                return configured;
            }

            SetState(LifecycleState.Configured, null);
            var started = await StepAsync(process, "start", () => process.Client.StartAsync(new StartRequest(), deadline: DateTime.UtcNow + _host.Options.StartTimeout, cancellationToken: cancellationToken).ResponseAsync, static r => r.Status).ConfigureAwait(false);
            if (started.IsFailure)
            {
                return started;
            }

            // 插件可在健康推送里再报 Started / Degraded；Start 返回即视为 Ready
            SetState(LifecycleState.Ready, null);
            LogStarted(Logger, Manifest.Id, process.ProcessId, _handshake?.Sdk ?? string.Empty);
            StartMonitoring(process);
            return Result.Success();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 启动进程并完成 <c>Handshake</c>；成功后进程保持运行、状态为 Resolved。
    /// </summary>
    private async Task<Result<HandshakeInfo>> LaunchAndHandshakeAsync(CancellationToken cancellationToken)
    {
        var token = _host.Sessions.Issue(this);
        var instance = $"{Manifest.Id}#{Interlocked.Increment(ref _instance).ToString(CultureInfo.InvariantCulture)}";
        var launch = new PluginLaunch(Manifest.Id, Executable, Directory, token, _host.HostEndpoint, instance, _host.Primitives, _host.DefaultLocale, _host.Options.HandshakeTimeout);
        var launched = await PluginProcess.LaunchAsync(launch, _host.Clock, Logger, cancellationToken).ConfigureAwait(false);
        if (!launched.TryGetValue(out var process))
        {
            _host.Sessions.Revoke(token);
            Fail(launched.Error!);
            return launched.Error!;
        }

        HandshakeResponse response;
        try
        {
            var request = new HandshakeRequest
            {
                Primitives = (uint)_host.Primitives,
                InstanceId = instance,
                Host = new HostInfo { FrameworkVersion = FrameworkInfo.Version, Sdk = "dotnet", SdkVersion = Environment.Version.ToString(), Application = _host.Application },
                HostEndpoint = new Endpoint { Network = _host.HostEndpoint.Network, Address = _host.HostEndpoint.Address },
                DefaultLocale = _host.DefaultLocale,
            };
            request.ProtocolVersions.AddRange(PluginProtocol.SupportedVersions);
            response = await process.Client.HandshakeAsync(request, deadline: DateTime.UtcNow + _host.Options.HandshakeTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException exception)
        {
            await DiscardAsync(process).ConfigureAwait(false);
            var error = exception.StatusCode == StatusCode.DeadlineExceeded
                ? Timeout("handshake")
                : new Error(PluginErrors.InvalidHandshake, PluginMessages.HandshakeInvalid, MessageArguments.Of(("id", Manifest.Id), ("reason", $"{exception.StatusCode}: {exception.Status.Detail}")));
            Fail(error);
            return error;
        }

        var validated = HandshakeValidator.Validate(Manifest, response, _host.Primitives);
        if (!validated.TryGetValue(out var info))
        {
            await DiscardAsync(process).ConfigureAwait(false);
            Fail(validated.Error!);
            return validated.Error!;
        }

        if (_handshake is { } previous && !previous.IsSameAs(info))
        {
            // 重启后的声明必须与首次相同：代理操作与订阅已经按首次登记
            await DiscardAsync(process).ConfigureAwait(false);
            var error = new Error(PluginErrors.HandshakeMismatch, PluginMessages.HandshakeMismatch, MessageArguments.Of(("id", Manifest.Id), ("reason", "the declarations changed since the plugin was first loaded")));
            Fail(error);
            return error;
        }

        _handshake ??= info;
        _process = process;
        SetState(LifecycleState.Resolved, null);
        LogHandshake(Logger, Manifest.Id, info.ProtocolVersion, info.Operations.Count, info.Subscriptions.Count);
        return info;
    }

    /// <summary>
    /// 执行一个生命周期调用（配置 / 启动）：gRPC 失败或插件报失败都丢弃进程并记为 Failed。
    /// </summary>
    private async Task<Result> StepAsync<TResponse>(PluginProcess process, string phase, Func<Task<TResponse>> call, Func<TResponse, Status?> status)
    {
        Error error;
        try
        {
            var response = await call().ConfigureAwait(false);
            var result = PluginErrorMapper.FromStatus(status(response), Manifest.Id, phase);
            if (result.IsSuccess)
            {
                return result;
            }

            error = Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, result.Error, MessageArguments.Of(("id", Manifest.Id), ("phase", phase), ("reason", result.Error!.Code)));
        }
        catch (RpcException exception)
        {
            error = exception.StatusCode == StatusCode.DeadlineExceeded ? Timeout(phase) : PluginErrorMapper.FromRpc(exception, Manifest.Id, phase, State);
        }

        await DiscardAsync(process).ConfigureAwait(false);
        Fail(error);
        return error;
    }

    /// <summary>
    /// 有序停止当前进程：取消监视、发 <c>Stop</c>、等宽限、必要时终止；最后进入 <paramref name="finalState"/>。
    /// </summary>
    private async Task ShutdownProcessAsync(string reason, LifecycleState finalState, CancellationToken cancellationToken)
    {
        if (_monitor is { } monitor)
        {
            _monitor = null;
            await monitor.CancelAsync().ConfigureAwait(false);
            monitor.Dispose();
        }

        var process = _process;
        _process = null;
        if (process is null)
        {
            SetState(finalState, null);
            return;
        }

        if (!process.HasExited)
        {
            SetState(LifecycleState.Draining, null);
            var grace = _host.StopGrace;
            try
            {
                await process.Client.StopAsync(new StopRequest { GraceSeconds = (uint)Math.Ceiling(grace.TotalSeconds), Reason = reason }, deadline: DateTime.UtcNow + grace, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException exception)
            {
                LogStopFailed(Logger, Manifest.Id, exception.StatusCode);
            }

            if (!await process.WaitForExitAsync(grace, _host.Clock, cancellationToken).ConfigureAwait(false))
            {
                LogStopTimeout(Logger, Manifest.Id, grace);
                await DiscardAsync(process).ConfigureAwait(false);
                Fail(Timeout("stop"));
                return;
            }
        }

        await DiscardAsync(process).ConfigureAwait(false);
        SetState(finalState, null);
    }

    /// <summary>
    /// 作废会话并释放进程（仍在运行则终止）。
    /// </summary>
    private async Task DiscardAsync(PluginProcess process)
    {
        _host.Sessions.Revoke(process.SessionToken);
        await process.DisposeAsync().ConfigureAwait(false);
        if (ReferenceEquals(_process, process))
        {
            _process = null;
        }
    }

    /// <summary>
    /// 开始监视进程：健康推送 / 轮询与退出。
    /// </summary>
    private void StartMonitoring(PluginProcess process)
    {
        var monitor = new CancellationTokenSource();
        _monitor = monitor;
        _ = WatchHealthAsync(process, monitor.Token);
        _ = WatchExitAsync(process, monitor);
    }

    /// <summary>
    /// 等待进程退出：监视未取消即为崩溃，记为 Failed 并按退避重启。
    /// </summary>
    private async Task WatchExitAsync(PluginProcess process, CancellationTokenSource monitor)
    {
        var exitCode = await process.Exited.ConfigureAwait(false);
        if (monitor.IsCancellationRequested || _stopping)
        {
            return;
        }

        await monitor.CancelAsync().ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(_process, process))
            {
                return;
            }

            await DiscardAsync(process).ConfigureAwait(false);
            Fail(Error.Unexpected(PluginErrors.Crashed, PluginMessages.ProcessCrashed, details: MessageArguments.Of(("id", Manifest.Id), ("exitCode", exitCode))));
            LogCrashed(Logger, Manifest.Id, exitCode);
            _restartPending = true;
        }
        finally
        {
            _gate.Release();
        }

        await RestartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 退避重启：1 秒起翻倍、上限 60 秒；时间窗内超过 <see cref="OutOfProcessPluginOptions.RestartLimit"/> 次后保持 Failed。
    /// </summary>
    private async Task RestartAsync()
    {
        var token = _lifetime.Token;
        while (!token.IsCancellationRequested)
        {
            int attempt;
            lock (_stateLock)
            {
                var now = _host.Clock.GetUtcNow();
                while (_restartTimes.TryPeek(out var oldest) && now - oldest > _host.Options.RestartWindow)
                {
                    _restartTimes.Dequeue();
                }

                attempt = _restartTimes.Count;
            }

            if (attempt >= _host.Options.RestartLimit)
            {
                _restartPending = false;
                LogRestartLimit(Logger, Manifest.Id, _host.Options.RestartLimit, _host.Options.RestartWindow);
                return;
            }

            var delay = TimeSpan.FromSeconds(Math.Min(_maxBackoff.TotalSeconds, Math.Pow(2, attempt)));
            LogRestartScheduled(Logger, Manifest.Id, delay, attempt + 1);
            try
            {
                await Task.Delay(delay, _host.Clock, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            lock (_stateLock)
            {
                _restartTimes.Enqueue(_host.Clock.GetUtcNow());
                _restarts++;
            }

            _restartPending = false;
            Result restarted;
            try
            {
                restarted = await EnsureRunningAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (restarted.IsSuccess)
            {
                LogRestarted(Logger, Manifest.Id, _restarts);
                return;
            }

            _restartPending = true;
        }
    }

    /// <summary>
    /// 健康监视：先订阅推送，推送断开后回退为轮询；连续失败达到阈值即终止进程交给退出监视处理。
    /// </summary>
    private async Task WatchHealthAsync(PluginProcess process, CancellationToken token)
    {
        try
        {
            using var call = process.Client.WatchHealth(new HealthRequest(), cancellationToken: token);
            await foreach (var health in call.ResponseStream.ReadAllAsync(token).ConfigureAwait(false))
            {
                if (!Apply(process, health))
                {
                    return;
                }
            }
        }
        catch (RpcException exception) when (!token.IsCancellationRequested)
        {
            LogHealthStreamLost(Logger, Manifest.Id, exception.StatusCode);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var failures = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_host.Options.HealthInterval, _host.Clock, token).ConfigureAwait(false);
                var health = await process.Client.HealthAsync(new HealthRequest(), deadline: DateTime.UtcNow + _host.Options.HealthInterval, cancellationToken: token).ConfigureAwait(false);
                failures = 0;
                if (!Apply(process, health))
                {
                    return;
                }
            }
            catch (RpcException) when (!token.IsCancellationRequested)
            {
                if (++failures >= _host.Options.HealthFailures)
                {
                    LogHealthLost(Logger, Manifest.Id, failures);
                    await process.KillAsync().ConfigureAwait(false);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 采纳一次健康应答；插件自报 Failed 时终止进程（退出监视随后按崩溃重启）。返回进程是否仍可继续监视。
    /// </summary>
    private bool Apply(PluginProcess process, HealthResponse health)
    {
        if (_stopping)
        {
            return false;
        }

        var error = health.Error is { } reported ? PluginErrorMapper.FromProto(reported, Manifest.Id) : null;
        var unhealthyItems = health.Items.Any(static item => item.Status is HealthStatus.Degraded or HealthStatus.Unhealthy);
        switch (health.State)
        {
            case ProtoLifecycleState.Failed:
                SetState(LifecycleState.Failed, error);
                _ = process.KillAsync();
                return false;
            case ProtoLifecycleState.Degraded:
                SetState(LifecycleState.Degraded, error);
                break;
            case ProtoLifecycleState.Ready:
                SetState(unhealthyItems ? LifecycleState.Degraded : LifecycleState.Ready, unhealthyItems ? error : null);
                break;
            case ProtoLifecycleState.Started:
                SetState(LifecycleState.Started, null);
                break;
            case ProtoLifecycleState.Draining:
                SetState(LifecycleState.Draining, null);
                break;
            default:
                // 其余状态由宿主裁决，插件自报无效
                break;
        }

        return true;
    }

    /// <summary>
    /// 组装 <c>Configure</c> 请求：配置节 JSON、已授予权限、已解析绑定与工作目录。
    /// </summary>
    private ConfigureRequest BuildConfigure()
    {
        var workDirectory = Path.Combine(_host.WorkRoot, Manifest.Id);
        System.IO.Directory.CreateDirectory(workDirectory);
        var request = new ConfigureRequest
        {
            Configuration = Manifest.Contributes.Config is { } config ? PluginJson.SerializeSection(_host.Configuration.GetSection(config.Section)) : "{}",
            WorkDirectory = workDirectory,
        };
        request.GrantedPermissions.AddRange(Manifest.Permissions);
        foreach (var binding in Manifest.Bindings)
        {
            // 与内核的绑定解析同源：XiHan:Bindings:<name> 的 value / type 子键，或节本身的标量值
            var section = _host.Configuration.GetSection($"{BindingResolver.ConfigurationSection}:{binding.Name}");
            var value = section["value"] ?? section.Value;
            if (value is not null)
            {
                request.Bindings.Add(new ProtoBinding { Name = binding.Name, Type = section["type"] ?? binding.Type, Value = value });
            }
        }

        return request;
    }

    /// <summary>
    /// 进入某状态。
    /// </summary>
    private void SetState(LifecycleState state, Error? error)
    {
        lock (_stateLock)
        {
            _state = state;
            _error = error;
        }

        LogState(Logger, Manifest.Id, state);
    }

    /// <summary>
    /// 进入 Failed。
    /// </summary>
    private void Fail(Error error) => SetState(LifecycleState.Failed, error);

    /// <summary>
    /// 插件不可用的失败。
    /// </summary>
    private Error Unavailable()
        => Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", Manifest.Id), ("state", State.ToString())));

    /// <summary>
    /// 某阶段超时。
    /// </summary>
    private Error Timeout(string phase)
        => new(PluginErrors.Timeout, PluginMessages.ProcessTimeout, MessageArguments.Of(("id", Manifest.Id), ("phase", phase)));

    /// <summary>记录握手完成。</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} completed the handshake with protocol v{ProtocolVersion}: {Operations} operations, {Subscriptions} subscriptions.")]
    private static partial void LogHandshake(ILogger logger, string pluginId, uint protocolVersion, int operations, int subscriptions);

    /// <summary>记录启动完成。</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} started (pid {ProcessId}, sdk {Sdk}).")]
    private static partial void LogStarted(ILogger logger, string pluginId, int processId, string sdk);

    /// <summary>记录状态变化。</summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin {PluginId} entered {State}.")]
    private static partial void LogState(ILogger logger, string pluginId, LifecycleState state);

    /// <summary>记录崩溃。</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} exited unexpectedly with code {ExitCode}.")]
    private static partial void LogCrashed(ILogger logger, string pluginId, int exitCode);

    /// <summary>记录重启排期。</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} will be restarted in {Delay} (attempt {Attempt}).")]
    private static partial void LogRestartScheduled(ILogger logger, string pluginId, TimeSpan delay, int attempt);

    /// <summary>记录重启成功。</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} restarted (total restarts: {Restarts}).")]
    private static partial void LogRestarted(ILogger logger, string pluginId, int restarts);

    /// <summary>记录重启次数耗尽。</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} reached the restart limit of {Limit} within {Window}; it stays failed until it is reloaded.")]
    private static partial void LogRestartLimit(ILogger logger, string pluginId, int limit, TimeSpan window);

    /// <summary>记录健康推送断开。</summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Health stream of plugin {PluginId} ended with {StatusCode}; falling back to polling.")]
    private static partial void LogHealthStreamLost(ILogger logger, string pluginId, StatusCode statusCode);

    /// <summary>记录健康探测连续失败。</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} failed {Failures} consecutive health probes; terminating the process.")]
    private static partial void LogHealthLost(ILogger logger, string pluginId, int failures);

    /// <summary>记录 Stop 调用失败。</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Stop call to plugin {PluginId} failed with {StatusCode}; waiting for the process to exit.")]
    private static partial void LogStopFailed(ILogger logger, string pluginId, StatusCode statusCode);

    /// <summary>记录停止超时。</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} did not exit within {Grace}; terminating the process.")]
    private static partial void LogStopTimeout(ILogger logger, string pluginId, TimeSpan grace);
}
