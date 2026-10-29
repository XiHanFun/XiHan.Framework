// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Security.OpenIdConnect;

/// <summary>
/// OpenID Connect 认证 Provider。
/// </summary>
/// <remarks>
/// 提供 <c>security.authentication@1</c>，注册 <c>Bearer</c> 处理器与 Authenticate 阶段中间件；授权仍由被选中的授权 Provider 承担。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OpenIdConnectSecurityFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.security.openidconnect",
        Package = "XiHan.Framework.Security.OpenIdConnect",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(SecurityCapabilities.Authentication, SecurityCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(AuthenticationMiddleware).FullName!, PipelineStage.Authenticate)],
            Config = new ConfigContribution("XiHan:Security"),
        },
        Entry = typeof(OpenIdConnectSecurityFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(SecurityMessages.Catalog);
        // SecurityOptions 用 TryAdd：与内存安全特性共存时只注册一次
        context.Services.TryAddSingleton(SecurityOptions.FromConfiguration(context.Configuration));
        var options = OpenIdConnectOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        // 元数据缓存用 TryAdd：测试或应用可预先注册自带 HTTP 客户端的实例
        context.Services.TryAddSingleton(provider => new OpenIdConnectMetadataCache(
            options,
            OpenIdConnectMetadataCache.CreateHttpClient(options),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IAuthenticationHandler, OpenIdConnectAuthenticationHandler>();
        context.AddMiddleware<AuthenticationMiddleware>(PipelineStage.Authenticate);
    }
}
