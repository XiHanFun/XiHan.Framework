// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// Wasm 插件宿主特性（Experimental）
/// </summary>
/// <remarks>
/// 配置阶段按 <c>XiHan:Plugins:Wasm:Directories</c> 发现插件（编译、实例化并握手），把声明的操作登记为代理操作、消息目录以清单 ID 为归属注册进容器、事件订阅接进容器、清单贡献的中间件按名字插进对应阶段（归属为插件的清单 ID，卸载时按归属撤回，ADR-0088）；启动阶段启动 <c>onStartup</c> 的插件；停止阶段停止全部插件
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed class WasmPluginsFeature : IHostedFeature
{
    /// <summary>
    /// 提供的能力
    /// </summary>
    public const string Capability = "plugins.wasm";

    /// <summary>
    /// 配置阶段创建的宿主
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
        Contributes = new Contributions { Config = new ConfigContribution(WasmPluginOptions.ConfigurationSection) },
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
        // 绑定根目录取应用的那一个：下发给插件的绑定与内核按同一套来源解析（ADR-0268）
        var host = new WasmPluginHost(options, context.Configuration, TimeProvider.System, NullLoggerFactory.Instance, localization.DefaultLocale, context.ServiceBindingRoot);
        host.UseRegistry(context.Operations);
        host.UsePipeline(context.Pipeline);
        _host = host;

        var subscribed = new HashSet<MessageSubscription>();
        foreach (var directory in options.Directories)
        {
            // 发现失败的插件留在宿主里以 Failed 上报，不阻止应用启动
            if (!host.Discover(directory).TryGetValue(out var runtime) || runtime.Handshake is null)
            {
                continue;
            }

            // 代理操作与权限码进登记表、目录按归属注册、订阅接进容器；登记不进去是配置错误，启动失败
            PluginContributions.Configure<WasmPluginEventHandler>(context, subscribed, runtime.Manifest, runtime.Handshake, runtime);

            // 配置阶段直接登记：约束目标可以是之后才发现的插件的中间件，在构建管道时解析
            WasmPluginHost.AddMiddleware(context.Pipeline, runtime);
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
