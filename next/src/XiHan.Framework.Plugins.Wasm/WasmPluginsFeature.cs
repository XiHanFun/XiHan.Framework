// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// Wasm 插件宿主特性（Experimental）。
/// </summary>
/// <remarks>
/// 配置阶段按 <c>XiHan:Plugins:Wasm:Directories</c> 发现插件（编译、实例化并握手），把声明的操作登记为代理操作、消息目录与事件订阅接进容器、清单贡献的中间件按名字插进对应阶段；启动阶段启动 <c>onStartup</c> 的插件；停止阶段停止全部插件。
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed class WasmPluginsFeature : IHostedFeature
{
    /// <summary>
    /// 提供的能力。
    /// </summary>
    public const string Capability = "plugins.wasm";

    /// <summary>
    /// 配置阶段创建的宿主。
    /// </summary>
    private WasmPluginHost? _host;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.plugins.wasm",
        Package = "XiHan.Framework.Plugins.Wasm",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Provides = [new CapabilityDeclaration(Capability, 1)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Plugins") },
        Entry = typeof(WasmPluginsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(PluginMessages.Catalog);
        var options = WasmPluginOptions.FromConfiguration(context.Configuration);
        var localization = LocalizationOptions.FromConfiguration(context.Configuration);
        // 容器尚未构建：先用空日志，附着时换成宿主的日志工厂
        var host = new WasmPluginHost(options, context.Configuration, TimeProvider.System, NullLoggerFactory.Instance, localization.DefaultLocale);
        host.UseRegistry(context.Operations);
        _host = host;

        var topics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in options.Directories)
        {
            // 发现失败的插件留在宿主里以 Failed 上报，不阻止应用启动
            if (!host.Discover(directory).TryGetValue(out var runtime) || runtime.Handshake is null)
            {
                continue;
            }

            // 与应用自己的操作重名是配置错误，启动失败；引用了未登记的权限码同样是配置错误，在装配完成时一并核对、启动失败
            context.Operations.AddRange(runtime.Handshake.Operations.Select(operation => (OperationDescriptor)new ProxyOperationDescriptor(operation.Contract, runtime.Manifest.Id, runtime, operation.Schema)).ToList()).ThrowIfFailure();

            foreach (var catalog in runtime.Handshake.Catalogs)
            {
                context.Services.AddMessageCatalog(catalog);
            }

            foreach (var subscription in runtime.Handshake.Subscriptions)
            {
                if (topics.Add(subscription.Topic))
                {
                    context.Services.AddMessageHandler<WasmPluginEventHandler>(subscription.Topic);
                }
            }

            UseMiddleware(context.Pipeline, runtime);
        }

        context.Services.AddSingleton(host);
        context.Services.AddSingleton<IPluginHost>(host);
        context.Services.AddSingleton<IHealthContributor>(host);
    }

    /// <summary>
    /// 把清单贡献的中间件插进对应阶段并声明相对次序。
    /// </summary>
    /// <remarks>
    /// 每个贡献项一个实例，以 <c>&lt;清单 ID&gt;:&lt;中间件名&gt;</c> 为名登记，归属为清单 ID（<c>spec/plugin/wasm.md</c> §6）。<c>before</c> / <c>after</c> 的每一项先按本插件的中间件名解析，否则原样作为管道里的中间件名（另一个插件的 <c>&lt;清单 ID&gt;:&lt;中间件名&gt;</c>，或只登记一次的宿主中间件类型全名）；目标在构建管道时解析，未登记、有歧义或跨阶段由内核报错。
    /// </remarks>
    private static void UseMiddleware(PipelineBuilder pipeline, WasmPluginRuntime runtime)
    {
        var contributions = runtime.Manifest.Contributes.Pipeline;
        var own = contributions.Select(static c => c.Middleware).ToHashSet(StringComparer.Ordinal);
        string Name(string middleware) => $"{runtime.Manifest.Id}:{middleware}";
        string Target(string reference) => own.Contains(reference) ? Name(reference) : reference;

        foreach (var contribution in contributions)
        {
            pipeline.UseNamedInstance(Name(contribution.Middleware), new WasmMiddleware(runtime, contribution.Middleware, contribution.Stage), contribution.Stage, Placement.End, runtime.Manifest.Id);
        }

        // 约束在本插件的中间件都登记之后声明：互相引用的两项不论清单里谁先谁后都能找到对方
        foreach (var contribution in contributions)
        {
            foreach (var later in contribution.Before)
            {
                pipeline.Before(Name(contribution.Middleware), Target(later));
            }

            foreach (var earlier in contribution.After)
            {
                pipeline.After(Name(contribution.Middleware), Target(earlier));
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
        => (_host ?? throw new InvalidOperationException("The feature has not been configured.")).AttachAsync(services, cancellationToken);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_host is { } host)
        {
            // 停止全部插件后整体释放：实例、引擎与 epoch 计时器都不再存活
            await host.StopAllAsync(cancellationToken).ConfigureAwait(false);
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
