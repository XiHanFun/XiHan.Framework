// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 应用实例：持有已解析图、特性运行时与管道；按拓扑顺序启动、逆序停止；聚合健康状态。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class App : IDisposable
{
    private static readonly ContextItemKey<PipelineStep> _handlerKey = new("xihan.handler");

    private readonly IServiceProvider _services;
    private readonly AppModel _model;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly Lazy<PipelineStep> _pipeline;

    internal App(IServiceProvider services, AppModel model, ILoggerFactory loggerFactory)
    {
        _services = services;
        _model = model;
        _logger = loggerFactory.CreateLogger<App>();
        _pipeline = new Lazy<PipelineStep>(() => model.Pipeline.Build(Terminal));
    }

    /// <summary>
    /// 创建应用实例。由容器调用。
    /// </summary>
    public App(IServiceProvider services, ILoggerFactory loggerFactory)
        : this(services, (services ?? throw new ArgumentNullException(nameof(services))).GetRequiredService<AppModel>(), loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)))
    {
    }

    /// <summary>
    /// 已解析图。
    /// </summary>
    public ResolvedGraph Graph => _model.Graph;

    /// <summary>
    /// 内核选项。
    /// </summary>
    public KernelOptions Options => _model.Options;

    /// <summary>
    /// 宿主解析出的绑定。
    /// </summary>
    public IReadOnlyDictionary<string, BindingValue> Bindings => _model.Bindings;

    /// <summary>
    /// 各特性的状态快照，按拓扑顺序。
    /// </summary>
    public IReadOnlyList<FeatureStatus> Features => _model.Features.Select(static f => f.Status).ToList();

    /// <summary>
    /// 应用聚合状态。
    /// </summary>
    public LifecycleState State => Aggregate(_model.Features.Select(static f => f.State).ToList(), _model.Options.FailurePolicy);

    /// <summary>
    /// 健康快照。
    /// </summary>
    public HealthSnapshot Health()
    {
        var features = Features;
        var state = State;
        var started = features.All(static f => f.State is >= LifecycleState.Started);
        return new HealthSnapshot(started, state.IsServing(), state != LifecycleState.Failed, state, features);
    }

    /// <summary>
    /// 已构建的管道委托；首次访问时构建。
    /// </summary>
    public PipelineStep Pipeline => _pipeline.Value;

    /// <summary>
    /// 打印已解析图。
    /// </summary>
    public string DescribeGraph() => _model.Graph.Describe();

    /// <summary>
    /// 打印管道最终顺序。
    /// </summary>
    public string DescribePipeline() => _model.Pipeline.Dump();

    /// <summary>
    /// 创建一次调用的上下文。未提供 Trace 时生成新的 W3C trace-id 与 parent-id。
    /// </summary>
    public static InvocationContext CreateContext(
        IServiceProvider scopedServices,
        CancellationToken cancellationToken,
        string? traceId = null,
        string? spanId = null,
        DateTimeOffset? deadline = null,
        IReadOnlyDictionary<string, string>? baggage = null)
    {
        ArgumentNullException.ThrowIfNull(scopedServices);
        return new InvocationContext
        {
            CancellationToken = cancellationToken,
            Deadline = deadline,
            TraceId = traceId ?? ActivityTraceId.CreateRandom().ToHexString(),
            SpanId = spanId ?? ActivitySpanId.CreateRandom().ToHexString(),
            Baggage = baggage ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Services = scopedServices,
        };
    }

    /// <summary>
    /// 让一次调用经过完整管道，<paramref name="handler"/> 在 Handle 阶段之后、所有中间件之内执行。
    /// </summary>
    public async ValueTask<Result> InvokeAsync(InvocationContext context, PipelineStep handler)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(handler);
        if (!State.IsServing())
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, $"应用处于 {State}，不能接收调用。");
        }

        context.Set(_handlerKey, handler);
        await Pipeline(context).ConfigureAwait(false);
        return context.GetOutcome().ValueOr(Result.Success());
    }

    /// <summary>
    /// 按拓扑顺序启动特性。失败按 <see cref="KernelOptions.FailurePolicy"/> 处理。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var graph = DescribeGraph();
                var pipeline = DescribePipeline();
                LogGraph(_logger, graph);
                LogPipeline(_logger, pipeline);
            }

            foreach (var runtime in _model.Features)
            {
                if (runtime.State is not LifecycleState.Configured and not LifecycleState.Resolved)
                {
                    continue;
                }

                if (runtime.State == LifecycleState.Resolved)
                {
                    runtime.Transition(LifecycleState.Configured);
                }

                await StartFeatureAsync(runtime, cancellationToken).ConfigureAwait(false);
            }

            LogState(_logger, State);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// 按逆序停止特性。超时的特性记为 <see cref="LifecycleState.Failed"/>，其余继续。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var i = _model.Features.Count - 1; i >= 0; i--)
            {
                var runtime = _model.Features[i];
                if (runtime.State is not (LifecycleState.Started or LifecycleState.Ready or LifecycleState.Degraded))
                {
                    continue;
                }

                await StopFeatureAsync(runtime, cancellationToken).ConfigureAwait(false);
            }

            LogState(_logger, State);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// 释放生命周期锁。由容器在应用停止后调用。
    /// </summary>
    public void Dispose() => _lifecycleLock.Dispose();

    private static ValueTask Terminal(InvocationContext context)
        => context.Get(_handlerKey).Match(handler => handler(context), static () => ValueTask.CompletedTask);

    private static LifecycleState Aggregate(List<LifecycleState> states, FailurePolicy policy)
    {
        if (states.Count == 0)
        {
            return LifecycleState.Ready;
        }

        if (states.Contains(LifecycleState.Failed) && policy == FailurePolicy.TerminateStartup)
        {
            return LifecycleState.Failed;
        }

        if (states.All(static s => s == LifecycleState.Stopped))
        {
            return LifecycleState.Stopped;
        }

        if (states.Contains(LifecycleState.Draining) || states.Contains(LifecycleState.Stopped))
        {
            return LifecycleState.Draining;
        }

        if (states.All(static s => s.IsServing()))
        {
            return states.Contains(LifecycleState.Degraded) ? LifecycleState.Degraded : LifecycleState.Ready;
        }

        if (states.Contains(LifecycleState.Failed))
        {
            // 隔离策略下有特性失败：其余就绪也不算就绪，报告为降级以便探针区分
            return states.Where(static s => s != LifecycleState.Failed).All(static s => s.IsServing()) ? LifecycleState.Degraded : states.Min();
        }

        return states.Min();
    }

    private async Task StartFeatureAsync(FeatureRuntime runtime, CancellationToken cancellationToken)
    {
        var id = runtime.Node.Manifest.Id;
        try
        {
            runtime.Transition(LifecycleState.Started);
            if (runtime.Feature is IHostedFeature hosted)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_model.Options.StartTimeout);
                await hosted.StartAsync(_services, timeout.Token).ConfigureAwait(false);
            }

            runtime.Transition(runtime.Node.IsDegraded ? LifecycleState.Degraded : LifecycleState.Ready);
            LogFeatureState(_logger, id, runtime.State);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var error = new Error(KernelErrors.FeatureStartFailed, $"特性 '{id}' 启动失败：{exception.Message}", null, exception is FrameworkException framework ? framework.ToError() : null);
            runtime.Fail(error);
            LogFeatureFailed(_logger, exception, id);

            if (_model.Options.FailurePolicy == FailurePolicy.TerminateStartup)
            {
                throw new FrameworkException(KernelErrors.FeatureStartFailed, error.Message, exception);
            }
        }
    }

    private async Task StopFeatureAsync(FeatureRuntime runtime, CancellationToken cancellationToken)
    {
        var id = runtime.Node.Manifest.Id;
        runtime.Transition(LifecycleState.Draining);
        try
        {
            if (runtime.Feature is IHostedFeature hosted)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_model.Options.StopTimeout);
                await hosted.StopAsync(timeout.Token).ConfigureAwait(false);
            }

            runtime.Transition(LifecycleState.Stopped);
            LogFeatureState(_logger, id, runtime.State);
        }
        catch (OperationCanceledException exception)
        {
            runtime.Fail(new Error(KernelErrors.FeatureStopTimeout, $"特性 '{id}' 停止超时。"));
            LogFeatureFailed(_logger, exception, id);
        }
        catch (Exception exception)
        {
            runtime.Fail(new Error(KernelErrors.FeatureStartFailed, $"特性 '{id}' 停止失败：{exception.Message}"));
            LogFeatureFailed(_logger, exception, id);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "能力图谱：\n{Graph}")]
    private static partial void LogGraph(ILogger logger, string graph);

    [LoggerMessage(Level = LogLevel.Information, Message = "管道：\n{Pipeline}")]
    private static partial void LogPipeline(ILogger logger, string pipeline);

    [LoggerMessage(Level = LogLevel.Information, Message = "特性 {FeatureId} 进入 {State}")]
    private static partial void LogFeatureState(ILogger logger, string featureId, LifecycleState state);

    [LoggerMessage(Level = LogLevel.Error, Message = "特性 {FeatureId} 失败")]
    private static partial void LogFeatureFailed(ILogger logger, Exception exception, string featureId);

    [LoggerMessage(Level = LogLevel.Information, Message = "应用状态 {State}")]
    private static partial void LogState(ILogger logger, LifecycleState state);
}
