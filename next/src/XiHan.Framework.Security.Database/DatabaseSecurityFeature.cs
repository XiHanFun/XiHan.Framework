// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authorization;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Database.Grants;

namespace XiHan.Framework.Security.Database;

/// <summary>
/// 数据库授权的能力包入口。
/// </summary>
/// <remarks>
/// 只提供 <c>security.authorization@1</c>（授予在数据库里），依赖 <c>persistence.relational@1</c>；认证仍由 <c>Security.InMemory</c>（ApiKey）、<c>Security.Jwt</c> 或 <c>Security.OpenIdConnect</c> 提供。装配后顶替默认内存实现的授权部分，内存实现只剩认证。被选为授权能力时在启动时核对操作契约引用的策略（ADR-0080）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseSecurityFeature : IHostedFeature
{
    /// <summary>
    /// 是否被选为授权能力（配置阶段记下，启动时据此核对策略引用）。
    /// </summary>
    private bool _authorizes;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.security.database",
        Package = "XiHan.Framework.Security.Database",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(SecurityCapabilities.Authorization, SecurityCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
        ],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(AuthorizationMiddleware).FullName!, PipelineStage.Authorize)],
            Config = new ConfigContribution(SecurityOptions.ConfigurationSection),
        },
        Entry = typeof(DatabaseSecurityFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(SecurityMessages.Catalog);
        // SecurityOptions 用 TryAdd：与 InMemory / Jwt 等其他安全特性共存时只注册一次
        context.Services.TryAddSingleton(SecurityOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton(DatabaseSecurityOptions.FromConfiguration(context.Configuration));
        context.Services.AddRepository<PermissionGrantRecord, Guid>();
        context.Services.AddSingleton<DatabasePermissionGrantStore>();
        context.Services.TryAddScoped<PermissionGrantRecordStore>();

        // 只为解析器选中本特性的能力注册服务与中间件；别的显式 Provider 胜出时本特性只留自己的类型
        if (context.Node.IsSelectedFor(SecurityCapabilities.Authorization))
        {
            context.Services.TryAddSingleton<IPermissionGrantStore>(static sp => sp.GetRequiredService<DatabasePermissionGrantStore>());
            context.Services.TryAddSingleton<IPermissionChecker, PermissionChecker>();
            context.Services.AddSingleton<IPolicy, AuthenticatedPolicy>();
            context.Services.TryAddSingleton<PolicyCatalog>();
            context.AddMiddleware<AuthorizationMiddleware>(PipelineStage.Authorize);
            _authorizes = true;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 被选为授权能力时核对操作契约按名引用的策略（ADR-0080）：策略是容器里的 <see cref="IPolicy"/>，容器建好才知道全部策略名，所以放在启动而不是配置阶段；引用了不存在的策略即 <see cref="SecurityErrors.UnregisteredPolicy"/>，本特性启动失败。
    /// </remarks>
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (_authorizes)
        {
            // 核对已登记的操作并挂到登记表上：此后插件握手登记的操作在登记时逐个核对，引用了不存在的策略整组被拒
            services.GetRequiredService<PolicyCatalog>().EnforceOn(services.GetRequiredService<OperationRegistry>());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
