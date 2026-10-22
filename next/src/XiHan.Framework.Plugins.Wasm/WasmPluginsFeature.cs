// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// Wasm 插件宿主特性（Experimental）：配置阶段按 <c>XiHan:Plugins:Wasm:Directories</c> 发现插件（编译、实例化并握手），把声明的操作登记为代理操作、
/// 消息目录与事件订阅接进容器、清单贡献的中间件按名字插进对应阶段；启动阶段启动 <c>onStartup</c> 的插件；停止阶段停止全部插件。
/// </summary>
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

            foreach (var operation in runtime.Handshake.Operations)
            {
                // 与应用自己的操作重名是配置错误，启动失败
                context.Operations.Add(new ProxyOperationDescriptor(operation.Contract, runtime.Manifest.Id, runtime, operation.Schema)).ThrowIfFailure();
            }

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

            foreach (var contribution in runtime.Manifest.Contributes.Pipeline)
            {
                // 每个贡献项一个实例，按"清单 ID:中间件名"登记，归属为清单 ID；相对约束按类型声明，Wasm 中间件不使用
                context.Pipeline.UseNamedInstance($"{runtime.Manifest.Id}:{contribution.Middleware}", new WasmMiddleware(runtime, contribution.Middleware, contribution.Stage), contribution.Stage, Placement.End, runtime.Manifest.Id);
            }
        }

        context.Services.AddSingleton(host);
        context.Services.AddSingleton<IPluginHost>(host);
        context.Services.AddSingleton<IHealthContributor>(host);
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
