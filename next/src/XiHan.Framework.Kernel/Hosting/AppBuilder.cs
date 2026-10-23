// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Operations;
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
    /// <summary>
    /// 通过 <see cref="UseFeature(IFeature)"/> 加入的特性，按加入顺序。
    /// </summary>
    private readonly List<IFeature> _features = [];

    /// <summary>
    /// 通过 <see cref="UseProvider"/> 在代码里显式指定的提供者：能力 ID → 清单 ID。
    /// </summary>
    private readonly Dictionary<string, string> _providerSelections = new(StringComparer.Ordinal);

    /// <summary>
    /// 通过 <see cref="UseOptions"/> 覆盖的内核选项；<see langword="null"/> 时从配置读取。
    /// </summary>
    private KernelOptions? _options;

    /// <summary>
    /// 通过 <see cref="UseServiceBindingRoot"/> 覆盖的 Service Binding 根目录；<see langword="null"/> 时读环境变量。
    /// </summary>
    private string? _serviceBindingRoot;

    /// <summary>
    /// 是否已调用过 <see cref="Build"/>；构建后禁止再修改。
    /// </summary>
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
        // 内核清单永远参与求解，它是所有 kernel.* 能力的唯一提供者，也是图谱的根。
        var manifests = new List<Manifest> { KernelCapabilities.Manifest };
        var featuresById = new Dictionary<string, IFeature>(StringComparer.Ordinal);
        foreach (var feature in _features)
        {
            var manifest = feature.Manifest ?? throw new FrameworkException(ErrorCode.InvalidArgument, $"Feature '{feature.GetType().FullName}' has a null manifest.");
            manifest.Validate().ThrowIfFailure();
            if (!featuresById.TryAdd(manifest.Id, feature))
            {
                throw new FrameworkException(KernelErrors.DuplicateManifest, $"Manifest ID '{manifest.Id}' is used by more than one feature.");
            }

            manifests.Add(manifest);
        }

        // 提供者指定的优先级：代码里的 UseProvider 覆盖配置节 XiHan:Providers，后写入者胜出。
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
        var operations = new OperationRegistry();

        // 内置中间件归属于内核清单并放在各自阶段开头，特性贡献的同阶段中间件都排在它们之后。
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

            // 必需绑定在构建期就校验，让缺失的资源在启动前暴露；可选绑定由特性自己通过 TryGetBinding 处理。
            foreach (var binding in node.Manifest.Bindings.Where(static b => b.Required))
            {
                if (!bindings.TryGetValue(binding.Name, out var value))
                {
                    throw new FrameworkException(KernelErrors.MissingBinding, $"Binding '{binding.Name}' ({binding.Type}) required by feature '{node.Manifest.Id}' is not provided.");
                }

                // 来源未声明类型（空字符串）时不做类型比对，只有显式声明且不一致才算错误。
                if (value.Type.Length > 0 && !string.Equals(value.Type, binding.Type, StringComparison.Ordinal))
                {
                    throw new FrameworkException(KernelErrors.MissingBinding, $"Binding '{binding.Name}' required by feature '{node.Manifest.Id}' must be of type '{binding.Type}', but the provided binding is of type '{value.Type}'.");
                }
            }

            feature.Configure(new FeatureContext(node, Services, Configuration, Pipeline, bindings, permissions, operations));
            runtimes.Add(new FeatureRuntime(node, feature));
        }

        // 宿主可能已经注册了时间源、日志与审计，这里只补缺省实现，不覆盖。
        Services.TryAddSingleton(TimeProvider.System);
        Services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        Services.TryAddSingleton<IAuditSink>(NullAuditSink.Instance);
        Services.AddSingleton(permissions);
        Services.AddSingleton(operations);
        Services.TryAddSingleton<IOperationInvoker, OperationInvoker>();
        // 当前调用的上下文访问器按作用域注册；宿主与测试基座一次调用一个作用域
        Services.TryAddScoped<InvocationAccessor>();
        Services.TryAddScoped<IInvocationAccessor>(static sp => sp.GetRequiredService<InvocationAccessor>());
        Services.AddSingleton(options);
        Services.AddSingleton(graph);
        Services.AddSingleton(new AppModel(graph, runtimes, Pipeline, options, bindings));
        Services.AddSingleton<App>();
        Services.AddHostedService<AppHostedService>();
        return graph;
    }

    /// <summary>
    /// 构建后再修改或重复构建时抛出 <see cref="KernelErrors.InvalidAppState"/>。
    /// </summary>
    private void EnsureNotBuilt()
    {
        if (_built)
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, "The AppBuilder has already been built and can no longer be modified or built again.");
        }
    }
}
