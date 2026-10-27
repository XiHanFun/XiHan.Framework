// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Wasmtime;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Plugins.Wasm.Instances;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Error = XiHan.Framework.Primitives.Error;
using LifecycleState = XiHan.Framework.Primitives.LifecycleState;
using ProtoBinding = XiHan.Framework.Plugins.Protocol.V1.Binding;
using ProtoLifecycleState = XiHan.Framework.Plugins.Protocol.V1.LifecycleState;

namespace XiHan.Framework.Plugins.Wasm.Runtime;

/// <summary>
/// 一个 Wasm 插件的运行时：模块编译、实例化与握手、配置与启动、串行调用、健康轮询、陷阱后的退避重建与停止（<c>spec/plugin/wasm.md</c> §5），以及经它的操作调用、事件投递与中间件钩子。
/// </summary>
internal sealed partial class WasmPluginRuntime : IPluginInvoker, IDisposable
{
    /// <summary>
    /// 重建退避上限。
    /// </summary>
    private static readonly TimeSpan _maxBackoff = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 宿主环境。
    /// </summary>
    private readonly WasmHostContext _host;

    /// <summary>
    /// 串行化实例的生命周期（实例化、配置、启动、停止、重建）。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 串行化对实例的调用：线性内存不是线程安全的。
    /// </summary>
    private readonly SemaphoreSlim _calls = new(1, 1);

    /// <summary>
    /// 保护状态字段的锁。
    /// </summary>
    private readonly Lock _stateLock = new();

    /// <summary>
    /// 时间窗内的重建时刻。
    /// </summary>
    private readonly Queue<DateTimeOffset> _restartTimes = new();

    /// <summary>
    /// 显式停止后取消：终止退避中的重建与健康轮询。
    /// </summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>
    /// 宿主导入函数。
    /// </summary>
    private readonly WasmHostFunctions _functions;

    /// <summary>
    /// 已编译的模块；首次发现时装载。
    /// </summary>
    private WasmModule? _module;

    /// <summary>
    /// 当前实例；没有时为空。
    /// </summary>
    private WasmInstance? _instance;

    /// <summary>
    /// 当前实例的监视（健康轮询）；停止时取消。
    /// </summary>
    private CancellationTokenSource? _monitor;

    /// <summary>
    /// 首次成功握手的结果；重建后核对。
    /// </summary>
    private HandshakeInfo? _handshake;

    /// <summary>
    /// 当前状态。
    /// </summary>
    private LifecycleState _state = LifecycleState.Declared;

    /// <summary>
    /// 当前错误。
    /// </summary>
    private Error? _error;

    /// <summary>
    /// 累计自动重建次数。
    /// </summary>
    private int _restarts;

    /// <summary>
    /// 实例序号，拼进实例 ID。
    /// </summary>
    private int _instanceNo;

    /// <summary>
    /// 已显式停止，不再启动也不再重建。
    /// </summary>
    private volatile bool _stopping;

    /// <summary>
    /// 崩溃后有重建在退避中；期间的调用直接按不可用失败。
    /// </summary>
    private volatile bool _restartPending;

    /// <summary>
    /// 已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 创建运行时。
    /// </summary>
    public WasmPluginRuntime(Manifest manifest, string directory, string modulePath, PermissionGrant permissions, IReadOnlyDictionary<string, string> mounts, WasmHostContext host, ILogger logger)
    {
        Manifest = manifest;
        Directory = directory;
        ModulePath = modulePath;
        Permissions = permissions;
        Mounts = mounts;
        _host = host;
        Logger = logger;
        _functions = new WasmHostFunctions(manifest, permissions, host, logger);
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
    /// 模块文件绝对路径。
    /// </summary>
    public string ModulePath { get; }

    /// <summary>
    /// 授予的权限。
    /// </summary>
    public PermissionGrant Permissions { get; }

    /// <summary>
    /// 文件系统映射：客体路径 → 宿主目录。
    /// </summary>
    public IReadOnlyDictionary<string, string> Mounts { get; }

    /// <summary>
    /// 日志；宿主附着到容器后换成真正的日志工厂产出的实例。
    /// </summary>
    public ILogger Logger { get; set; }

    /// <summary>
    /// 是否懒激活。
    /// </summary>
    public bool IsLazy => !Manifest.Activation.Contains("onStartup", StringComparer.Ordinal);

    /// <summary>
    /// 首次成功握手的结果。
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
    /// 实例存在且处于可服务状态。
    /// </summary>
    public bool IsServing => _instance is not null && State is LifecycleState.Started or LifecycleState.Ready or LifecycleState.Degraded;

    /// <summary>
    /// 诊断快照。
    /// </summary>
    public WasmPluginStatus Describe() => new(Status, _instance?.AbiVersion, _handshake?.ProtocolVersion, _restarts, IsLazy);

    /// <summary>
    /// 是否订阅了该主题与类型。
    /// </summary>
    public bool IsSubscribedTo(string topic, string messageType)
        => _handshake?.Subscriptions.Any(s => s.Matches(topic, messageType)) == true;

    /// <summary>
    /// 探测启动：编译模块、实例化并握手；懒激活的插件随即丢弃实例，其余保持实例等待 <see cref="StartAsync"/>。
    /// </summary>
    public async Task<Result<HandshakeInfo>> DiscoverAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var launched = await InstantiateAndHandshakeAsync(cancellationToken).ConfigureAwait(false);
            if (!launched.TryGetValue(out var info))
            {
                return launched.Error!;
            }

            if (IsLazy)
            {
                // 懒激活只为取得声明：实例化在毫秒级，首次调用时再建
                await DropInstanceAsync().ConfigureAwait(false);
                SetState(LifecycleState.Resolved, null);
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
    /// 显式停止：不再重建，调用 <c>xihan_stop</c> 后丢弃实例。
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
            if (_instance is { } instance && State is LifecycleState.Configured or LifecycleState.Started or LifecycleState.Ready or LifecycleState.Degraded)
            {
                SetState(LifecycleState.Draining, null);
                var stopped = await CallAsync<StopResponse>(instance, "xihan_stop", new StopRequest { GraceSeconds = (uint)Math.Ceiling(_host.Options.CallTimeout.TotalSeconds), Reason = reason }, _host.Options.CallTimeout, cancellationToken).ConfigureAwait(false);
                if (stopped.IsFailure)
                {
                    LogStopFailed(Logger, Manifest.Id, stopped.Error!.Code);
                }
            }

            await DropInstanceAsync().ConfigureAwait(false);
            SetState(LifecycleState.Stopped, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<object>> InvokeAsync(string operation, ReadOnlyMemory<byte> payload, InvocationContext context)
    {
        var acquired = await AcquireAsync(context.CancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var instance))
        {
            return acquired.Error!;
        }

        var request = new InvokeRequest { Operation = operation, Payload = ByteString.CopyFrom(payload.Span), Metadata = PluginMetadata.FromContext(context) };
        var response = await CallAsync<InvokeResponse>(instance, "xihan_invoke", request, Timeout(context), context.CancellationToken).ConfigureAwait(false);
        if (!response.TryGetValue(out var invoked))
        {
            return response.Error!;
        }

        return invoked.Status is { Ok: true }
            ? Result.Success<object>(new PluginPayload(invoked.Payload.Memory))
            : PluginErrorMapper.FromStatus(invoked.Status, Manifest.Id, operation).Error!;
    }

    /// <summary>
    /// 把一条消息投递给插件。
    /// </summary>
    public async ValueTask<Result> DeliverAsync(MessageDelivery delivery, InvocationContext context)
    {
        var acquired = await AcquireAsync(context.CancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var instance))
        {
            return acquired.Error!;
        }

        var request = new HandleEventRequest { Envelope = PluginEnvelopeMapper.ToProto(delivery.Topic, delivery.Envelope), Metadata = PluginMetadata.FromContext(context), Attempt = (uint)delivery.Attempt };
        var response = await CallAsync<HandleEventResponse>(instance, "xihan_handle_event", request, Timeout(context), context.CancellationToken).ConfigureAwait(false);
        return response.TryGetValue(out var handled) ? PluginErrorMapper.FromStatus(handled.Status, Manifest.Id, "event") : response.Error!;
    }

    /// <summary>
    /// 中间件钩子的一个阶段（<c>before</c> / <c>after</c>）。
    /// </summary>
    public async ValueTask<Result<MiddlewareResponse>> MiddlewareAsync(string middleware, PipelineStage stage, string phase, InvocationContext context, Result? outcome, CancellationToken cancellationToken)
    {
        var acquired = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var instance))
        {
            return acquired.Error!;
        }

        var request = new MiddlewareRequest
        {
            Middleware = middleware,
            Stage = stage.ToManifestName(),
            Phase = phase,
            Metadata = PluginMetadata.FromContext(context),
            Operation = context.GetOperation().TryGetValue(out var descriptor) ? descriptor.Id : string.Empty,
        };
        if (outcome is { } downstream)
        {
            request.Outcome = PluginErrorMapper.ToStatus(downstream);
        }

        return await CallAsync<MiddlewareResponse>(instance, "xihan_middleware", request, _host.Options.MiddlewareTimeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 释放同步原语与模块；调用前应已 <see cref="StopAsync"/>。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DropInstanceCore();
        _module?.Dispose();
        _lifetime.Dispose();
        _functions.Pump.Dispose();
        _calls.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// 取可服务的实例：没有在运行就按需启动（懒激活、停止后的首次调用）；退避中的重建不被绕过。
    /// </summary>
    private async Task<Result<WasmInstance>> AcquireAsync(CancellationToken cancellationToken)
    {
        if (IsServing)
        {
            return _instance!;
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

        return IsServing ? _instance! : Unavailable();
    }

    /// <summary>
    /// 保证实例存在并已 <c>Start</c>：必要时实例化、握手、配置、启动并开始健康轮询。
    /// </summary>
    private async Task<Result> EnsureRunningAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 拿到锁后再判一次：崩溃处理可能刚在锁内把实例丢掉并排了退避重建，等锁的调用不能抢在退避前重建
            if (_stopping || _restartPending)
            {
                return Unavailable();
            }

            if (IsServing)
            {
                return Result.Success();
            }

            if (_instance is null)
            {
                var launched = await InstantiateAndHandshakeAsync(cancellationToken).ConfigureAwait(false);
                if (launched.IsFailure)
                {
                    return launched.Error!;
                }
            }

            var instance = _instance!;
            var configured = await StepAsync<ConfigureResponse>(instance, "xihan_configure", BuildConfigure(), static r => r.Status, "configure", cancellationToken).ConfigureAwait(false);
            if (configured.IsFailure)
            {
                return configured;
            }

            SetState(LifecycleState.Configured, null);
            var started = await StepAsync<StartResponse>(instance, "xihan_start", new StartRequest(), static r => r.Status, "start", cancellationToken).ConfigureAwait(false);
            if (started.IsFailure)
            {
                return started;
            }

            SetState(LifecycleState.Ready, null);
            LogStarted(Logger, Manifest.Id, instance.AbiVersion, _handshake?.Sdk ?? string.Empty);
            StartMonitoring(instance);
            return Result.Success();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 编译（首次）、实例化并握手；成功后实例保持、状态为 Resolved。
    /// </summary>
    private async Task<Result<HandshakeInfo>> InstantiateAndHandshakeAsync(CancellationToken cancellationToken)
    {
        if (_module is null)
        {
            var loaded = WasmModule.Load(_host.Engine, ModulePath, Manifest.Id);
            if (!loaded.TryGetValue(out var module))
            {
                Fail(loaded.Error!);
                return loaded.Error!;
            }

            var consistency = CheckDeclarations(module);
            if (consistency.IsFailure)
            {
                module.Dispose();
                Fail(consistency.Error!);
                return consistency.Error!;
            }

            _module = module;
        }

        WasmInstance instance;
        try
        {
            var workDirectory = Path.Combine(_host.WorkRoot, Manifest.Id);
            System.IO.Directory.CreateDirectory(workDirectory);
            instance = await Task.Run(() => new WasmInstance(_host.Engine, _module, _functions, BuildWasi(workDirectory), _host.Options.MemoryLimitBytes, _host.Options.EpochInterval, _host.Options.CallTimeout), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is WasmtimeException or InvalidOperationException)
        {
            var error = Error.Unexpected(PluginErrors.Crashed, PluginMessages.ModuleTrapped, details: MessageArguments.Of(("id", Manifest.Id), ("trap", exception.Message)));
            Fail(error);
            return error;
        }

        if (!PluginProtocol.SupportedVersions.Contains(instance.AbiVersion))
        {
            instance.Dispose();
            var error = new Error(PluginErrors.InvalidModule, PluginMessages.ModuleInvalid, MessageArguments.Of(("id", Manifest.Id), ("reason", $"ABI version {instance.AbiVersion} is not supported")));
            Fail(error);
            return error;
        }

        _instance = instance;
        var handshake = await CallAsync<HandshakeResponse>(instance, "xihan_handshake", BuildHandshake(), _host.Options.CallTimeout, cancellationToken).ConfigureAwait(false);
        if (!handshake.TryGetValue(out var response))
        {
            await DropInstanceAsync().ConfigureAwait(false);
            Fail(handshake.Error!);
            return handshake.Error!;
        }

        var validated = HandshakeValidator.Validate(Manifest, response, _host.Primitives, PluginIsolation.Wasm);
        if (validated.TryGetValue(out var info) && info.Operations.Any(static o => o.Streaming))
        {
            validated = new Error(PluginErrors.HandshakeMismatch, PluginMessages.HandshakeMismatch, MessageArguments.Of(("id", Manifest.Id), ("reason", "streaming operations are not supported by the Wasm carrier")));
        }

        if (!validated.TryGetValue(out info))
        {
            await DropInstanceAsync().ConfigureAwait(false);
            Fail(validated.Error!);
            return validated.Error!;
        }

        if (_handshake is { } previous && !previous.IsSameAs(info))
        {
            await DropInstanceAsync().ConfigureAwait(false);
            var error = new Error(PluginErrors.HandshakeMismatch, PluginMessages.HandshakeMismatch, MessageArguments.Of(("id", Manifest.Id), ("reason", "the declarations changed since the plugin was first loaded")));
            Fail(error);
            return error;
        }

        if (info.Subscriptions.Count > 0 && !_module.HasEventHandler)
        {
            await DropInstanceAsync().ConfigureAwait(false);
            var error = new Error(PluginErrors.InvalidModule, PluginMessages.ModuleInvalid, MessageArguments.Of(("id", Manifest.Id), ("reason", "the module declares subscriptions but does not export 'xihan_handle_event'")));
            Fail(error);
            return error;
        }

        _handshake ??= info;
        SetState(LifecycleState.Resolved, null);
        LogHandshake(Logger, Manifest.Id, info.ProtocolVersion, info.Operations.Count, info.Subscriptions.Count);
        return info;
    }

    /// <summary>
    /// 清单与模块导出的一致性：贡献了中间件必须导出 <c>xihan_middleware</c>，中间件阶段不得是 <c>handle</c>。
    /// </summary>
    private Result CheckDeclarations(WasmModule module)
    {
        if (Manifest.Contributes.Pipeline.Count > 0 && !module.HasMiddleware)
        {
            return new Error(PluginErrors.InvalidModule, PluginMessages.ModuleInvalid, MessageArguments.Of(("id", Manifest.Id), ("reason", "the manifest contributes middleware but the module does not export 'xihan_middleware'")));
        }

        foreach (var contribution in Manifest.Contributes.Pipeline)
        {
            if (contribution.Stage == PipelineStage.Handle)
            {
                return new Error(PluginErrors.InvalidModule, PluginMessages.ModuleInvalid, MessageArguments.Of(("id", Manifest.Id), ("reason", $"middleware '{contribution.Middleware}' must not be contributed to the handle stage")));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 执行一个生命周期调用（配置 / 启动）：失败丢弃实例并记为 Failed。
    /// </summary>
    private async Task<Result> StepAsync<TResponse>(WasmInstance instance, string export, IMessage request, Func<TResponse, Status?> status, string phase, CancellationToken cancellationToken)
        where TResponse : IMessage<TResponse>, new()
    {
        var called = await CallAsync<TResponse>(instance, export, request, _host.Options.CallTimeout, cancellationToken).ConfigureAwait(false);
        Error error;
        if (called.TryGetValue(out var response))
        {
            var result = PluginErrorMapper.FromStatus(status(response), Manifest.Id, phase);
            if (result.IsSuccess)
            {
                return result;
            }

            error = Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, result.Error, MessageArguments.Of(("id", Manifest.Id), ("phase", phase), ("reason", result.Error!.Code)));
        }
        else
        {
            error = called.Error!;
        }

        await DropInstanceAsync().ConfigureAwait(false);
        Fail(error);
        return error;
    }

    /// <summary>
    /// 串行地做一次调用并解码响应；陷阱、超时与无响应都让实例失效（在途的崩溃处理由 <see cref="HandleCrashAsync"/> 负责）。
    /// </summary>
    private async Task<Result<TResponse>> CallAsync<TResponse>(WasmInstance instance, string export, IMessage request, TimeSpan timeout, CancellationToken cancellationToken)
        where TResponse : IMessage<TResponse>, new()
    {
        var encoded = PluginJsonCodec.Encode(request);
        WasmCallOutcome outcome;
        var reentrant = _functions.Pump.IsReentrant;
        if (reentrant && !ReferenceEquals(_instance, instance))
        {
            return Unavailable();
        }

        // 插件自己的回调引发的再入（回调 → 宿主管道 → 本插件的中间件、操作或订阅）：调用锁由外层调用持有，
        // 等它会死锁，所以回到执行线程上、在导入函数返回前同步完成，受外层调用的截止时间约束；外层已结束时走普通路径
        if (!reentrant || !_functions.Pump.TryRunNested(() => instance.CallNested(export, encoded), out outcome))
        {
            await _calls.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(_instance, instance))
                {
                    return Unavailable();
                }

                // Wasm 执行是同步的 CPU 工作：放到线程池，不占用调用方的异步链
                outcome = await Task.Run(() => instance.Call(export, encoded, timeout), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _calls.Release();
            }
        }

        switch (outcome.Status)
        {
            case WasmCallStatus.Success:
                var decoded = PluginJsonCodec.Decode<TResponse>(outcome.Response);
                if (decoded is null)
                {
                    var invalid = Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, details: MessageArguments.Of(("id", Manifest.Id), ("phase", export), ("reason", "the response is not a valid JSON message")));
                    Background(() => HandleCrashAsync(instance, invalid));
                    return invalid;
                }

                return decoded;
            case WasmCallStatus.Exhausted:
                return Error.Unavailable(PluginErrors.ResourceExhausted, PluginMessages.SandboxExhausted, MessageArguments.Of(("id", Manifest.Id), ("reason", outcome.Detail)));
            case WasmCallStatus.Timeout:
                // 中断后客体状态不可信：按崩溃重建，调用方得到超时
                Background(() => HandleCrashAsync(instance, new Error(PluginErrors.Timeout, PluginMessages.ProcessTimeout, MessageArguments.Of(("id", Manifest.Id), ("phase", export)))));
                return new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded);
            default:
                var crashed = Error.Unexpected(PluginErrors.Crashed, PluginMessages.ModuleTrapped, details: MessageArguments.Of(("id", Manifest.Id), ("trap", outcome.Detail)));
                Background(() => HandleCrashAsync(instance, crashed));
                return crashed;
        }
    }

    /// <summary>
    /// 启动后台工作：同步前缀立刻执行（崩溃标记要先于调用方拿到结果），之后的延续不带当前异步流——回调流的标记不能漏进健康轮询与崩溃重建，否则它们的调用会被当成再入。
    /// </summary>
    private static void Background(Func<Task> work)
    {
        using (ExecutionContext.SuppressFlow())
        {
            _ = work();
        }
    }

    /// <summary>
    /// 运行期崩溃：若该实例仍是当前实例则丢弃、记为 Failed 并按退避重建；生命周期阶段的失败由各自的路径处理，这里不重复。
    /// </summary>
    private async Task HandleCrashAsync(WasmInstance instance, Error error)
    {
        if (_stopping)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(_instance, instance) || State is LifecycleState.Declared or LifecycleState.Resolved or LifecycleState.Configured)
            {
                return;
            }

            // 先排退避重建再丢实例：同一次调用的 after 阶段等中间件会紧跟着取实例，不能让它抢先重建
            _restartPending = true;
            await DropInstanceAsync().ConfigureAwait(false);
            Fail(error);
            LogCrashed(Logger, Manifest.Id, error.Message);
        }
        finally
        {
            _gate.Release();
        }

        await RestartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 退避重建：1 秒起翻倍、上限 60 秒；时间窗内超过 <see cref="WasmPluginOptions.RestartLimit"/> 次后保持 Failed。
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
    /// 开始健康轮询。
    /// </summary>
    private void StartMonitoring(WasmInstance instance)
    {
        _monitor?.Dispose();
        var monitor = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _monitor = monitor;
        Background(() => HealthLoopAsync(instance, monitor.Token));
    }

    /// <summary>
    /// 每 <see cref="WasmPluginOptions.HealthInterval"/> 调一次 <c>xihan_health</c>；连续失败达到阈值即按崩溃处理。
    /// </summary>
    private async Task HealthLoopAsync(WasmInstance instance, CancellationToken token)
    {
        var failures = 0;
        while (!token.IsCancellationRequested && ReferenceEquals(_instance, instance))
        {
            try
            {
                await Task.Delay(_host.Options.HealthInterval, _host.Clock, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!ReferenceEquals(_instance, instance) || _stopping)
            {
                return;
            }

            var health = await CallAsync<HealthResponse>(instance, "xihan_health", new HealthRequest(), _host.Options.CallTimeout, token).ConfigureAwait(false);
            if (health.TryGetValue(out var response))
            {
                failures = 0;
                if (!Apply(instance, response))
                {
                    return;
                }
            }
            else if (++failures >= _host.Options.HealthFailures)
            {
                LogHealthLost(Logger, Manifest.Id, failures);
                await HandleCrashAsync(instance, health.Error!).ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>
    /// 采纳一次健康应答；插件自报 Failed 时按崩溃处理。返回是否继续轮询。
    /// </summary>
    private bool Apply(WasmInstance instance, HealthResponse health)
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
                Background(() => HandleCrashAsync(instance, error ?? Error.Unexpected(PluginErrors.Crashed, PluginMessages.ModuleTrapped, details: MessageArguments.Of(("id", Manifest.Id), ("trap", "the plugin reported Failed")))));
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
                break;
        }

        return true;
    }

    /// <summary>
    /// 组装握手请求：没有回调端点，回调经导入函数。
    /// </summary>
    private HandshakeRequest BuildHandshake()
    {
        var request = new HandshakeRequest
        {
            Primitives = (uint)_host.Primitives,
            InstanceId = $"{Manifest.Id}#{Interlocked.Increment(ref _instanceNo).ToString(CultureInfo.InvariantCulture)}",
            Host = new HostInfo { FrameworkVersion = FrameworkInfo.Version, Sdk = "dotnet", SdkVersion = Environment.Version.ToString(), Application = _host.Application },
            DefaultLocale = _host.DefaultLocale,
        };
        request.ProtocolVersions.AddRange(PluginProtocol.SupportedVersions);
        return request;
    }

    /// <summary>
    /// 组装 <c>Configure</c> 请求：配置节 JSON、已授予权限（含已映射的文件系统权限）、已解析绑定与工作目录。
    /// </summary>
    private ConfigureRequest BuildConfigure()
    {
        var workDirectory = Path.Combine(_host.WorkRoot, Manifest.Id);
        var request = new ConfigureRequest
        {
            Configuration = Manifest.Contributes.Config is { } config ? PluginJson.SerializeSection(_host.Configuration.GetSection(config.Section)) : "{}",
            WorkDirectory = workDirectory,
        };
        request.GrantedPermissions.AddRange(Manifest.Permissions);
        foreach (var binding in Manifest.Bindings)
        {
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
    /// WASI 配置：协议约定的环境变量、stdout / stderr 写到工作目录、按权限预开放目录。
    /// </summary>
    private WasiConfiguration BuildWasi(string workDirectory)
    {
        var wasi = new WasiConfiguration()
            .WithEnvironmentVariable(PluginProtocol.PrimitivesVariable, _host.Primitives.ToString(CultureInfo.InvariantCulture))
            .WithEnvironmentVariable(PluginProtocol.ProtocolVersionsVariable, string.Join(',', PluginProtocol.SupportedVersions.Select(static v => v.ToString(CultureInfo.InvariantCulture))))
            .WithEnvironmentVariable(PluginProtocol.InstanceVariable, $"{Manifest.Id}#{_instanceNo.ToString(CultureInfo.InvariantCulture)}")
            .WithEnvironmentVariable(PluginProtocol.LocaleVariable, _host.DefaultLocale)
            .WithStandardOutput(Path.Combine(workDirectory, "stdout.log"))
            .WithStandardError(Path.Combine(workDirectory, "stderr.log"));
        foreach (var permission in Manifest.Permissions)
        {
            // fs.read:<客体路径> / fs.write:<客体路径>：发现阶段已确认映射存在
            if (permission.StartsWith("fs.read:", StringComparison.Ordinal) && Mounts.TryGetValue(permission["fs.read:".Length..], out var readable))
            {
                wasi = wasi.WithPreopenedDirectory(readable, permission["fs.read:".Length..], mutable: false);
            }
            else if (permission.StartsWith("fs.write:", StringComparison.Ordinal) && Mounts.TryGetValue(permission["fs.write:".Length..], out var writable))
            {
                wasi = wasi.WithPreopenedDirectory(writable, permission["fs.write:".Length..], mutable: true);
            }
        }

        return wasi;
    }

    /// <summary>
    /// 调用的时限：上下文的剩余时间，没有截止时间时取调用时限。
    /// </summary>
    private TimeSpan Timeout(InvocationContext context)
        => context.RemainingTime(_host.Clock.GetUtcNow()) is { } remaining && remaining > TimeSpan.Zero && remaining < _host.Options.CallTimeout ? remaining : _host.Options.CallTimeout;

    /// <summary>
    /// 丢弃当前实例并停止它的健康轮询；在调用锁内释放，Store 不会在别的线程执行 Wasm 时被释放。
    /// </summary>
    private async Task DropInstanceAsync()
    {
        await _calls.WaitAsync().ConfigureAwait(false);
        try
        {
            DropInstanceCore();
        }
        finally
        {
            _calls.Release();
        }
    }

    /// <summary>
    /// 丢弃当前实例（调用方已持有调用锁，或运行时正在释放）。
    /// </summary>
    private void DropInstanceCore()
    {
        if (_monitor is { } monitor)
        {
            _monitor = null;
            monitor.Cancel();
            monitor.Dispose();
        }

        var instance = _instance;
        _instance = null;
        instance?.Dispose();
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
    /// 记录握手完成。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Wasm plugin {PluginId} completed the handshake with protocol v{ProtocolVersion}: {Operations} operations, {Subscriptions} subscriptions.")]
    private static partial void LogHandshake(ILogger logger, string pluginId, uint protocolVersion, int operations, int subscriptions);

    /// <summary>
    /// 记录启动完成。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Wasm plugin {PluginId} started (ABI v{AbiVersion}, sdk {Sdk}).")]
    private static partial void LogStarted(ILogger logger, string pluginId, uint abiVersion, string sdk);

    /// <summary>
    /// 记录状态变化。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Wasm plugin {PluginId} entered {State}.")]
    private static partial void LogState(ILogger logger, string pluginId, LifecycleState state);

    /// <summary>
    /// 记录崩溃。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Wasm plugin {PluginId} crashed: {Reason}")]
    private static partial void LogCrashed(ILogger logger, string pluginId, string reason);

    /// <summary>
    /// 记录重建排期。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Wasm plugin {PluginId} will be rebuilt in {Delay} (attempt {Attempt}).")]
    private static partial void LogRestartScheduled(ILogger logger, string pluginId, TimeSpan delay, int attempt);

    /// <summary>
    /// 记录重建成功。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Wasm plugin {PluginId} rebuilt (total restarts: {Restarts}).")]
    private static partial void LogRestarted(ILogger logger, string pluginId, int restarts);

    /// <summary>
    /// 记录重建次数耗尽。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Wasm plugin {PluginId} reached the restart limit of {Limit} within {Window}; it stays failed until it is reloaded.")]
    private static partial void LogRestartLimit(ILogger logger, string pluginId, int limit, TimeSpan window);

    /// <summary>
    /// 记录健康探测连续失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Wasm plugin {PluginId} failed {Failures} consecutive health probes; rebuilding the instance.")]
    private static partial void LogHealthLost(ILogger logger, string pluginId, int failures);

    /// <summary>
    /// 记录 Stop 调用失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Stop call to Wasm plugin {PluginId} failed with {Code}; dropping the instance.")]
    private static partial void LogStopFailed(ILogger logger, string pluginId, string code);
}
