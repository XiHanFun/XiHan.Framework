// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Localization.Database;

/// <summary>
/// 数据库本地化的能力包入口：提供 <c>localization.resources@1</c>，依赖 <c>persistence.relational@1</c>。装配后顶替默认的内存本地化。
/// 健康项 <see cref="HealthName"/> 由 <see cref="MessageTranslationLoader"/> 以最近一次加载的结果报告：失败（上一份快照仍在生效）为 Degraded。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseLocalizationFeature : IHostedFeature
{
    /// <summary>
    /// 健康项名：翻译最近一次加载是否成功。
    /// </summary>
    public const string HealthName = "localization.database.translations";

    /// <summary>
    /// 启动后保存的加载器，供停止时使用。
    /// </summary>
    private MessageTranslationLoader? _loader;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.localization.database",
        Package = "XiHan.Framework.Localization.Database",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(LocalizationCapabilities.Resources, LocalizationCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
        ],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Localization"),
            Health = [HealthName],
        },
        Entry = typeof(DatabaseLocalizationFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(LocalizationMessages.Catalog);
        context.Services.AddSingleton(LocalizationOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton(DatabaseLocalizationOptions.FromConfiguration(context.Configuration));
        // 原语层与内核不能读嵌入资源，它们的语言文件由本地化 Provider 代为装载；同名目录只注册一次
        context.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("primitives", typeof(Error)));
        context.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("kernel", typeof(App)));
        context.Services.AddRepository<MessageTranslation, Guid>();
        context.Services.TryAddScoped<MessageTranslationStore>();
        context.Services.AddSingleton<DatabaseMessageLocalizer>();
        context.Services.AddSingleton<MessageTranslationLoader>();
        // 加载器自己就是健康项的贡献者：它最清楚最近一次加载成功与否，不另起探测循环
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredService<MessageTranslationLoader>());
        if (context.Node.IsSelectedFor(LocalizationCapabilities.Resources))
        {
            // 同一个实例同时作为本地化器（显式区域）与解析器（环境作用域）
            context.Services.TryAddSingleton<IMessageLocalizer>(static sp => sp.GetRequiredService<DatabaseMessageLocalizer>());
            context.Services.TryAddSingleton<IMessageResolver>(static sp => sp.GetRequiredService<DatabaseMessageLocalizer>());
        }
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 进程默认作用域用默认区域：日志、异常与 Error.Message 都按它渲染；调用链里的区域由内核按 Baggage 另行安装
        MessageScope.SetDefault(services.GetRequiredService<IMessageResolver>(), services.GetRequiredService<LocalizationOptions>().DefaultLocale);
        _loader = services.GetRequiredService<MessageTranslationLoader>();
        _loader.Start();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // 停止后不清除默认作用域：停机日志仍需要文案；下一个启动的应用会覆盖它
        if (_loader is not null)
        {
            await _loader.StopAsync().ConfigureAwait(false);
            _loader = null;
        }
    }
}
