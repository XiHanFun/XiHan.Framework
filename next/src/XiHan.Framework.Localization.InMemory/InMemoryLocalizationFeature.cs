// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Localization.InMemory;

/// <summary>
/// 内存本地化的能力包入口。
/// </summary>
/// <remarks>
/// 默认提供 <c>localization.resources@1</c>，注册选项、原语层与内核的语言文件目录与本地化器；启动时把本地化器设为进程默认的 <see cref="MessageScope"/>，让 <c>key.L()</c> 与 <see cref="Error.Message"/> 在任何地方都有默认语言文案。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryLocalizationFeature : IHostedFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.localization.inmemory",
        Package = "XiHan.Framework.Localization.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(LocalizationCapabilities.Resources, LocalizationCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Localization") },
        Entry = typeof(InMemoryLocalizationFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton(LocalizationOptions.FromConfiguration(context.Configuration));
        // 原语层与内核不能读嵌入资源，它们的语言文件由本特性代为装载；各子系统目录由其 Provider 注册，应用目录由应用注册
        context.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("primitives", typeof(Error)));
        context.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("kernel", typeof(App)));
        // 同一个实例同时作为本地化器（显式区域）与解析器（环境作用域）
        context.Services.TryAddSingleton<MessageLocalizer>();
        context.Services.TryAddSingleton<IMessageLocalizer>(static sp => sp.GetRequiredService<MessageLocalizer>());
        context.Services.TryAddSingleton<IMessageResolver>(static sp => sp.GetRequiredService<MessageLocalizer>());
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 进程默认作用域用默认区域：日志、异常与 Error.Message 都按它渲染；调用链里的区域由内核按 Baggage 另行安装
        MessageScope.SetDefault(services.GetRequiredService<IMessageResolver>(), services.GetRequiredService<LocalizationOptions>().DefaultLocale);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // 停止后不清除默认作用域：停机日志仍需要文案；下一个启动的应用会覆盖它
        return Task.CompletedTask;
    }
}
