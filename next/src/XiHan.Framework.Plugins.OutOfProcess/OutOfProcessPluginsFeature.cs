// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.OutOfProcess.Runtime;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 进程外插件宿主特性。
/// </summary>
/// <remarks>
/// 配置阶段按 <c>XiHan:Plugins:OutOfProcess:Directories</c> 发现插件（启动进程并握手），把声明的操作登记为代理操作、消息目录与事件订阅接进容器；启动阶段启动回调服务器与 <c>onStartup</c> 的插件；停止阶段停止全部插件。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OutOfProcessPluginsFeature : IHostedFeature
{
    /// <summary>
    /// 提供的能力。
    /// </summary>
    public const string Capability = "plugins.out-of-process";

    /// <summary>
    /// 配置阶段创建的宿主。
    /// </summary>
    private OutOfProcessPluginHost? _host;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.plugins.outofprocess",
        Package = "XiHan.Framework.Plugins.OutOfProcess",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Provides = [new CapabilityDeclaration(Capability, 1)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution(OutOfProcessPluginOptions.ConfigurationSection) },
        Entry = typeof(OutOfProcessPluginsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(PluginMessages.Catalog);
        var options = OutOfProcessPluginOptions.FromConfiguration(context.Configuration);
        var kernel = KernelOptions.FromConfiguration(context.Configuration);
        var localization = LocalizationOptions.FromConfiguration(context.Configuration);
        // 容器尚未构建：先用空日志，附着时换成宿主的日志工厂
        var host = new OutOfProcessPluginHost(options, context.Configuration, TimeProvider.System, NullLoggerFactory.Instance, kernel.StopTimeout, localization.DefaultLocale);
        host.UseRegistry(context.Operations);
        _host = host;

        var subscribed = new HashSet<MessageSubscription>();
        foreach (var directory in options.Directories)
        {
            // 发现失败的插件留在宿主里以 Failed 上报，不阻止应用启动
            if (!host.Discover(directory).TryGetValue(out var runtime) || runtime.Handshake is null)
            {
                continue;
            }

            // 清单声明的权限码连同代理操作一次登记（ADR-0080）：声明的码与已登记的重复、操作与应用自己的重名都是配置错误，启动失败；
            // 引用了未登记的权限码同样是配置错误，在装配完成时一并核对、启动失败（插件声明的码此时已在登记表里）
            PluginContributions.Register(context.Operations, runtime.Manifest, runtime.Handshake, runtime).ThrowIfFailure();

            foreach (var catalog in runtime.Handshake.Catalogs)
            {
                context.Services.AddMessageCatalog(catalog);
            }

            // 每个（主题，方式）登记一次转交处理器，实际投给哪个插件由宿主按握手声明决定
            foreach (var subscription in runtime.Handshake.Subscriptions)
            {
                if (subscribed.Add(new MessageSubscription(subscription.Topic, subscription.Mode)))
                {
                    context.Services.AddMessageHandler<PluginEventHandler>(subscription.Topic, subscription.Mode);
                }
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
            // 停止全部插件后整体释放：进程、回调服务器与同步原语都不再存活
            await host.StopAllAsync(cancellationToken).ConfigureAwait(false);
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
