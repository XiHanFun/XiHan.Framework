// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Observability.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 运行信息的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>observability.host@1</c>：被选中时注册单例 <see cref="IHostMonitor"/>，启动时同步采第一次样并起采样循环，停止时结束循环。选项见 <see cref="HostObservabilityOptions"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HostObservabilityFeature : IHostedFeature
{
    /// <summary>
    /// 启动后的采样器。
    /// </summary>
    private HostSampler? _sampler;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.observability.host",
        Package = "XiHan.Framework.Observability.Host",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(ObservabilityCapabilities.Host, ObservabilityCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Observability") },
        Entry = typeof(HostObservabilityFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = HostObservabilityOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        if (!context.Node.IsSelectedFor(ObservabilityCapabilities.Host))
        {
            return;
        }

        context.Services.AddSingleton(static _ => HostPlatform.Create());
        context.Services.AddSingleton(static sp => new HostSampler(
            sp.GetRequiredService<HostPlatform>(),
            sp.GetRequiredService<HostObservabilityOptions>().SampleInterval,
            sp.GetService<TimeProvider>() ?? TimeProvider.System,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<HostSampler>(),
            Environment.ProcessorCount));
        context.Services.AddSingleton<IHostMonitor>(static sp => new HostMonitor(sp.GetRequiredService<HostPlatform>(), sp.GetRequiredService<HostSampler>(), sp.GetService<TimeProvider>() ?? TimeProvider.System));
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.GetService<HostSampler>() is { } sampler)
        {
            await sampler.StartAsync(cancellationToken).ConfigureAwait(false);
            _sampler = sampler;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_sampler is not null)
        {
            await _sampler.StopAsync(cancellationToken).ConfigureAwait(false);
            _sampler = null;
        }
    }
}
