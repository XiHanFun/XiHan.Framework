// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 应用构建器：收集特性、求解能力图谱、按拓扑顺序配置特性、把 <see cref="App"/> 与宿主服务注册进容器。
/// 容器由宿主构建；内核只配置 <see cref="IServiceCollection"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class AppBuilder
{
    private readonly List<IFeature> _features = [];
    private readonly Dictionary<string, string> _providerSelections = new(StringComparer.Ordinal);
    private KernelOptions? _options;
    private string? _serviceBindingRoot;
    private bool _built;

    /// <summary>
    /// 创建构建器。
    /// </summary>
    public AppBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Pipeline = new PipelineBuilder();
    }

    /// <summary>
    /// 宿主的服务集合。
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 宿主配置。
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// 应用的管道构建器；特性与应用都向它贡献中间件。
    /// </summary>
    public PipelineBuilder Pipeline { get; }

    /// <summary>
    /// 加入一个特性。
    /// </summary>
    public AppBuilder UseFeature<TFeature>()
        where TFeature : class, IFeature, new()
        => UseFeature(new TFeature());

    /// <summary>
    /// 加入一个已创建的特性实例。
    /// </summary>
    public AppBuilder UseFeature(IFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        EnsureNotBuilt();
        _features.Add(feature);
        return this;
    }

    /// <summary>
    /// 显式指定某个能力由哪份清单提供；优先于配置 <c>XiHan:Providers:&lt;能力ID&gt;</c>。
    /// </summary>
    public AppBuilder UseProvider(string capabilityId, string manifestId)
    {
        EnsureNotBuilt();
        _providerSelections[CapabilityId.Validate(capabilityId)] = ManifestId.Validate(manifestId);
        return this;
    }

    /// <summary>
    /// 覆盖从配置读取的内核选项。
    /// </summary>
    public AppBuilder UseOptions(KernelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureNotBuilt();
        _options = options;
        return this;
    }

    /// <summary>
    /// 覆盖 Service Binding 根目录（默认读环境变量 <c>SERVICE_BINDING_ROOT</c>）。
    /// </summary>
    public AppBuilder UseServiceBindingRoot(string? directory)
    {
        EnsureNotBuilt();
        _serviceBindingRoot = directory;
        return this;
    }

    /// <summary>
    /// 应用级地向管道贡献中间件。
    /// </summary>
    public AppBuilder ConfigurePipeline(Action<PipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnsureNotBuilt();
        configure(Pipeline);
        return this;
    }

    /// <summary>
    /// 求解能力图谱、配置特性并注册 <see cref="App"/>。只能调用一次。
    /// </summary>
    /// <returns>已解析图。</returns>
    public ResolvedGraph Build()
    {
        EnsureNotBuilt();
        _built = true;

        var options = _options ?? KernelOptions.FromConfiguration(Configuration);
        var manifests = new List<Manifest> { KernelCapabilities.Manifest };
        var featuresById = new Dictionary<string, IFeature>(StringComparer.Ordinal);
        foreach (var feature in _features)
        {
            var manifest = feature.Manifest ?? throw new FrameworkException(ErrorCode.InvalidArgument, $"特性 {feature.GetType().FullName} 的清单为 null。");
            manifest.Validate().ThrowIfFailure();
            if (!featuresById.TryAdd(manifest.Id, feature))
            {
                throw new FrameworkException(KernelErrors.DuplicateManifest, $"清单 ID '{manifest.Id}' 被多个特性使用。");
            }

            manifests.Add(manifest);
        }

        var selections = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var section in Configuration.GetSection("XiHan:Providers").GetChildren())
        {
            if (section.Value is not null)
            {
                selections[section.Key] = section.Value;
            }
        }

        foreach (var (capability, manifestId) in _providerSelections)
        {
            selections[capability] = manifestId;
        }

        var graph = CapabilityResolver.Resolve(manifests, selections);
        var bindings = BindingResolver.Resolve(Configuration, _serviceBindingRoot);
        var permissions = new PermissionRegistry();

        if (options.UseBuiltInMiddleware)
        {
            Services.TryAddSingleton<ObserveMiddleware>();
            Services.TryAddSingleton<DeadlineMiddleware>();
            Pipeline.UseAt<ObserveMiddleware>(PipelineStage.Observe, Placement.Beginning, KernelCapabilities.Manifest.Id);
            Pipeline.UseAt<DeadlineMiddleware>(PipelineStage.Resilience, Placement.Beginning, KernelCapabilities.Manifest.Id);
        }

        var runtimes = new List<FeatureRuntime>();
        foreach (var node in graph.Nodes)
        {
            // 声明了能力却一个都没被选中的节点（被显式 Provider 顶替的 default 等）不装配、不启动
            if (node.IsShadowed || !featuresById.TryGetValue(node.Manifest.Id, out var feature))
            {
                continue;
            }

            foreach (var binding in node.Manifest.Bindings.Where(static b => b.Required))
            {
                if (!bindings.TryGetValue(binding.Name, out var value))
                {
                    throw new FrameworkException(KernelErrors.MissingBinding, $"特性 '{node.Manifest.Id}' 需要的绑定 '{binding.Name}'（{binding.Type}）未提供。");
                }

                if (value.Type.Length > 0 && !string.Equals(value.Type, binding.Type, StringComparison.Ordinal))
                {
                    throw new FrameworkException(KernelErrors.MissingBinding, $"特性 '{node.Manifest.Id}' 需要的绑定 '{binding.Name}' 类型为 {binding.Type}，提供的是 {value.Type}。");
                }
            }

            feature.Configure(new FeatureContext(node, Services, Configuration, Pipeline, bindings, permissions));
            runtimes.Add(new FeatureRuntime(node, feature));
        }

        Services.TryAddSingleton(TimeProvider.System);
        Services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        Services.TryAddSingleton<IAuditSink>(NullAuditSink.Instance);
        Services.AddSingleton(permissions);
        Services.AddSingleton(options);
        Services.AddSingleton(graph);
        Services.AddSingleton(new AppModel(graph, runtimes, Pipeline, options, bindings));
        Services.AddSingleton<App>();
        Services.AddHostedService<AppHostedService>();
        return graph;
    }

    private void EnsureNotBuilt()
    {
        if (_built)
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, "AppBuilder 已经构建，不能再修改或重复构建。");
        }
    }
}

/// <summary>
/// 构建产物：已解析图、特性运行时、管道与选项。由 <see cref="App"/> 消费。
/// </summary>
internal sealed record AppModel(
    ResolvedGraph Graph,
    IReadOnlyList<FeatureRuntime> Features,
    PipelineBuilder Pipeline,
    KernelOptions Options,
    IReadOnlyDictionary<string, BindingValue> Bindings);

/// <summary>
/// 把 <see cref="App"/> 接入 Generic Host 的生命周期。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "由容器通过 AddHostedService 实例化。")]
internal sealed class AppHostedService(App app) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => app.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => app.StopAsync(cancellationToken);
}

/// <summary>
/// <see cref="IHostApplicationBuilder"/> 的接入点。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// 在宿主构建器上装配 XiHan 应用：共享宿主的服务集合与配置，<see cref="App"/> 随宿主启动与停止。
    /// </summary>
    public static TBuilder AddXiHan<TBuilder>(this TBuilder builder, Action<AppBuilder> configure)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var appBuilder = new AppBuilder(builder.Services, builder.Configuration);
        configure(appBuilder);
        appBuilder.Build();
        return builder;
    }
}
