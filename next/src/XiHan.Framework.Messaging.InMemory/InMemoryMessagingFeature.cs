// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Messaging.InMemory;

/// <summary>
/// 内存消息的能力包入口：默认提供 <c>messaging.queue@1</c>；启动时消费全部订阅主题，停止时等待进行中的投递结束。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryMessagingFeature : IHostedFeature
{
    /// <summary>
    /// 启动后取得的运行时；停止时用它结束消费。
    /// </summary>
    private MessagingRuntime? _runtime;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.messaging.inmemory",
        Package = "XiHan.Framework.Messaging.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(MessagingCapabilities.Queue, MessagingCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Messaging"),
            Health = ["messaging.inmemory.capacity"],
        },
        Entry = typeof(InMemoryMessagingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(MessagingMessages.Catalog);
        context.Services.AddSingleton(InMemoryMessagingOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<InMemoryMessageBroker>();
        context.Services.TryAddSingleton<IMessageBroker>(static sp => sp.GetRequiredService<InMemoryMessageBroker>());
        MessagingRegistration.Configure(context);
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _runtime = services.GetRequiredService<MessagingRuntime>();
        await _runtime.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_runtime is not null)
        {
            await _runtime.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
