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
/// 应用实例。
/// </summary>
/// <remarks>
/// 持有已解析图、特性运行时与管道；按拓扑顺序启动、逆序停止；聚合健康状态。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class App : IDisposable
{
    /// <summary>
    /// 调用处理器在 <see cref="InvocationContext.Items"/> 中的键。
    /// </summary>
    /// <remarks>
    /// <see cref="InvokeAsync"/> 写入，<see cref="Terminal"/> 读取。
    /// </remarks>
    private static readonly ContextItemKey<PipelineStep> _handlerKey = new("xihan.handler");

    /// <summary>
    /// 根服务提供器，传给特性的 <see cref="IHostedFeature.StartAsync"/>。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 构建产物：已解析图、特性运行时、管道与选项。
    /// </summary>
    private readonly AppModel _model;

    /// <summary>
    /// 应用的日志器。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 串行化启动与停止，防止两者交错执行。
    /// </summary>
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    /// <summary>
    /// 延迟构建的管道委托。
    /// </summary>
    /// <remarks>
    /// 首次访问 <see cref="Pipeline"/> 时构建并缓存。
    /// </remarks>
    private readonly Lazy<PipelineStep> _pipeline;

    /// <summary>
    /// 创建应用实例。
    /// </summary>
    /// <remarks>
    /// 供公开构造函数与测试直接传入已构建的模型。
    /// </remarks>
    /// <param name="services">根服务提供器。</param>
    /// <param name="model">构建产物。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    internal App(IServiceProvider services, AppModel model, ILoggerFactory loggerFactory)
    {
        _services = services;
        _model = model;
        _logger = loggerFactory.CreateLogger<App>();
        _pipeline = new Lazy<PipelineStep>(() => model.Pipeline.Build(Terminal));
    }

    /// <summary>
    /// 创建应用实例。
    /// </summary>
    /// <remarks>
    /// 由容器调用。
    /// </remarks>
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
    public LifecycleState State => Degrade(Aggregate(_model.Features.Select(static f => f.State).ToList(), _model.Options.FailurePolicy), Contributions);

    /// <summary>
    /// 各 <see cref="IHealthContributor"/> 的条目状态。
    /// </summary>
    /// <remarks>
    /// 贡献者抛出的异常不传播，记为该贡献者整体 Failed。
    /// </remarks>
    public IReadOnlyList<ContributorStatus> Contributions
    {
        get
        {
            var contributions = new List<ContributorStatus>();
            foreach (var contributor in _services.GetServices<IHealthContributor>())
            {
                try
                {
                    contributions.AddRange(contributor.Check());
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // 健康采集不能因为某个贡献者出错而失败：把异常本身记成一条 Failed 条目
                    contributions.Add(new ContributorStatus(contributor.GetType().FullName ?? contributor.GetType().Name, LifecycleState.Failed, Error.Unexpected(KernelErrors.Unexpected, KernelMessages.UnhandledException, exception is FrameworkException framework ? framework.ToError() : null)));
                }
            }

            return contributions;
        }
    }

    /// <summary>
    /// 健康快照。
    /// </summary>
    public HealthSnapshot Health()
    {
        var features = Features;
        var contributions = Contributions;
        var state = Degrade(Aggregate(features.Select(static f => f.State).ToList(), _model.Options.FailurePolicy), contributions);
        var started = features.All(static f => f.State is >= LifecycleState.Started);
        return new HealthSnapshot(started, state.IsServing(), state != LifecycleState.Failed, state, features, contributions);
    }

    /// <summary>
    /// 贡献条目只把 Ready 压成 Degraded。
    /// </summary>
    /// <remarks>
    /// 条目 Degraded / Failed 表示应用仍在服务但有部件不健康，不改变其他聚合状态。
    /// </remarks>
    private static LifecycleState Degrade(LifecycleState state, IReadOnlyList<ContributorStatus> contributions)
        => state == LifecycleState.Ready && contributions.Any(static c => c.State is LifecycleState.Degraded or LifecycleState.Failed)
            ? LifecycleState.Degraded
            : state;

    /// <summary>
    /// 已构建的管道委托。
    /// </summary>
    /// <remarks>
    /// 首次访问时构建。
    /// </remarks>
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
    /// 创建一次调用的上下文。
    /// </summary>
    /// <remarks>
    /// 未提供 Trace 时生成新的 W3C trace-id 与 parent-id。
    /// </remarks>
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
            throw new FrameworkException(KernelErrors.InvalidAppState, $"The application is in state '{State}' and cannot accept invocations.");
        }

        // 处理器通过上下文传给终点而不是闭包捕获：管道委托只构建一次，对所有调用共享。
        context.Set(_handlerKey, handler);

        // 调用链深处不传上下文的服务经 IInvocationAccessor 取当前上下文：进入管道前设置，离开时恢复上一层（同一作用域内的嵌套调用）
        var accessor = context.Services.GetService<InvocationAccessor>();
        using var current = accessor is null ? default : accessor.Enter(context);

        // 整个调用链里 key.L() 按调用方区域解析：解析器取自本次调用的服务作用域（没有装配本地化时沿用进程默认），区域取自 Baggage
        using var messages = MessageScope.Use(
            context.Services.GetService<IMessageResolver>(),
            context.TryGetBaggage(BaggageKeys.Locale, out var locale) ? locale : null);
        await Pipeline(context).ConfigureAwait(false);
        return context.GetOutcome().ValueOr(Result.Success());
    }

    /// <summary>
    /// 按拓扑顺序启动特性。
    /// </summary>
    /// <remarks>
    /// 失败按 <see cref="KernelOptions.FailurePolicy"/> 处理。
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 图谱与管道的打印有成本，只在信息级别启用时才生成。
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var graph = DescribeGraph();
                var pipeline = DescribePipeline();
                LogGraph(_logger, graph);
                LogPipeline(_logger, pipeline);
            }

            foreach (var runtime in _model.Features)
            {
                // 只启动尚未启动过的特性：已失败、已停止或已就绪的跳过，使重复调用 StartAsync 幂等。
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
    /// 按逆序停止特性。
    /// </summary>
    /// <remarks>
    /// 超时的特性记为 <see cref="LifecycleState.Failed"/>，其余继续。
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 逆拓扑序：依赖者先停，被依赖者后停，停止期间依赖仍可用。
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
    /// 释放生命周期锁。
    /// </summary>
    /// <remarks>
    /// 由容器在应用停止后调用。
    /// </remarks>
    public void Dispose() => _lifecycleLock.Dispose();

    /// <summary>
    /// 管道终点。
    /// </summary>
    /// <remarks>
    /// 执行 <see cref="InvokeAsync"/> 放入上下文的处理器；没有处理器时什么都不做。
    /// </remarks>
    private static ValueTask Terminal(InvocationContext context)
        => context.Get(_handlerKey).Match(handler => handler(context), static () => ValueTask.CompletedTask);

    /// <summary>
    /// 把各特性状态聚合为应用状态。
    /// </summary>
    /// <remarks>
    /// 判定顺序：失败且终止策略 → 全部停止 → 任一排空或停止 → 全部可服务 → 隔离策略下有失败 → 最保守状态。
    /// </remarks>
    /// <param name="states">各特性的状态。</param>
    /// <param name="policy">启动失败策略，决定单个特性失败是否拖垮整个应用。</param>
    private static LifecycleState Aggregate(List<LifecycleState> states, FailurePolicy policy)
    {
        // 没有任何特性的应用没有东西可失败，直接视为就绪。
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

        // 停止过程中只要有一个特性开始排空或已停止，整个应用就不再接收新调用。
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

        // 其余情况（启动进行中）取最早的状态，表示应用还没走到该阶段。
        return states.Min();
    }

    /// <summary>
    /// 启动一个特性。
    /// </summary>
    /// <remarks>
    /// 进入 Started，带启动超时调用 <see cref="IHostedFeature.StartAsync"/>，再按是否有缺失的可选依赖进入 Ready 或 Degraded。失败时记录错误；终止策略下重新抛出 <see cref="KernelErrors.FeatureStartFailed"/>。
    /// </remarks>
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
            // 宿主自己的取消（外部 token 已触发）不算特性失败，原样向上传播；启动超时属于特性失败。
            var error = new Error(KernelErrors.FeatureStartFailed, KernelMessages.FeatureStartFailed, MessageArguments.Of(("id", id), ("reason", exception.Message)), exception is FrameworkException framework ? framework.ToError() : null);
            runtime.Fail(error);
            LogFeatureFailed(_logger, exception, id);

            if (_model.Options.FailurePolicy == FailurePolicy.TerminateStartup)
            {
                throw new FrameworkException(KernelErrors.FeatureStartFailed, error.Message, exception);
            }
        }
    }

    /// <summary>
    /// 停止一个特性。
    /// </summary>
    /// <remarks>
    /// 进入 Draining，带停止超时调用 <see cref="IHostedFeature.StopAsync"/>，成功后进入 Stopped。超时记为 <see cref="KernelErrors.FeatureStopTimeout"/>，其他异常记为失败；两者都不中断其余特性的停止。
    /// </remarks>
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
            runtime.Fail(new Error(KernelErrors.FeatureStopTimeout, KernelMessages.FeatureStopTimeout, MessageArguments.Of(("id", id))));
            LogFeatureFailed(_logger, exception, id);
        }
        catch (Exception exception)
        {
            runtime.Fail(new Error(KernelErrors.FeatureStartFailed, KernelMessages.FeatureStopFailed, MessageArguments.Of(("id", id), ("reason", exception.Message))));
            LogFeatureFailed(_logger, exception, id);
        }
    }

    /// <summary>
    /// 在启动时打印已解析图。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Capability graph:\n{Graph}")]
    private static partial void LogGraph(ILogger logger, string graph);

    /// <summary>
    /// 在启动时打印管道最终顺序。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Pipeline:\n{Pipeline}")]
    private static partial void LogPipeline(ILogger logger, string pipeline);

    /// <summary>
    /// 记录特性进入的新状态。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Feature '{FeatureId}' entered state '{State}'.")]
    private static partial void LogFeatureState(ILogger logger, string featureId, LifecycleState state);

    /// <summary>
    /// 记录特性启动或停止失败及其异常。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Feature '{FeatureId}' failed.")]
    private static partial void LogFeatureFailed(ILogger logger, Exception exception, string featureId);

    /// <summary>
    /// 记录启动或停止完成后的应用聚合状态。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Application state is '{State}'.")]
    private static partial void LogState(ILogger logger, LifecycleState state);
}
