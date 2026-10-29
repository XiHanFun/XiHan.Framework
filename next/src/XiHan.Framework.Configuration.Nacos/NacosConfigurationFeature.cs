// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// Nacos 远程配置的能力包入口。
/// </summary>
/// <remarks>
/// 显式 Provider，提供 <c>configuration.remote@1</c>，把 <see cref="NacosConfigurationClient"/> 登记为 <see cref="IRemoteConfigurationClient"/> 供运行期取用。配置构建期（没有容器）接进 <c>IConfigurationBuilder</c> 用 <see cref="NacosConfigurationBuilderExtensions.AddXiHanNacos"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class NacosConfigurationFeature : IFeature
{
    /// <summary>
    /// 连接绑定名。
    /// </summary>
    /// <remarks>
    /// <c>nacos</c>，类型 <c>configuration:remote</c>，可选；值是服务地址。
    /// </remarks>
    public const string ConnectionBinding = "nacos";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.configuration.nacos",
        Package = "XiHan.Framework.Configuration.Nacos",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(ConfigurationCapabilities.Remote, ConfigurationCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Bindings = [new Binding(ConnectionBinding, "configuration:remote", required: false)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Configuration"),
        },
        Entry = typeof(NacosConfigurationFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(ConfigurationMessages.Catalog);
        var options = NacosOptions.FromConfiguration(context.Configuration, context.TryGetBinding(ConnectionBinding));
        context.Services.AddSingleton(options);
        // 超时由客户端按请求控制（长轮询要比默认的 100 秒窗口更可控）
        context.Services.AddHttpClient(NacosConfigurationClient.HttpClientName, static client => client.Timeout = Timeout.InfiniteTimeSpan);
        context.Services.AddSingleton(static sp => new NacosConfigurationClient(
            sp.GetRequiredService<NacosOptions>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(NacosConfigurationClient.HttpClientName),
            sp.GetService<TimeProvider>() ?? TimeProvider.System));
        if (context.Node.IsSelectedFor(ConfigurationCapabilities.Remote))
        {
            context.Services.TryAddSingleton<IRemoteConfigurationClient>(static sp => sp.GetRequiredService<NacosConfigurationClient>());
        }
    }
}
