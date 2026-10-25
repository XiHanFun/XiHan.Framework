// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Security.Jwt;

/// <summary>
/// JWT 认证的能力包入口：提供 <c>security.authentication@1</c>，注册 Bearer 处理器与 Authenticate 阶段中间件。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JwtSecurityFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.security.jwt",
        Package = "XiHan.Framework.Security.Jwt",
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
        Entry = typeof(JwtSecurityFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(SecurityMessages.Catalog);
        // SecurityOptions 用 TryAdd：与内存安全特性共存时只注册一次
        context.Services.TryAddSingleton(SecurityOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton(JwtOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<IAuthenticationHandler, JwtAuthenticationHandler>();
        context.AddMiddleware<AuthenticationMiddleware>(PipelineStage.Authenticate);
    }
}
