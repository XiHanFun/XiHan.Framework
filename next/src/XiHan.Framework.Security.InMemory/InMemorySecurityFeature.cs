// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.InMemory;

/// <summary>
/// 内存安全的能力包入口：默认提供 <c>security.authentication@1</c>（ApiKey）与 <c>security.authorization@1</c>（RBAC 授予）。
/// 只为被选中的能力注册服务与中间件。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemorySecurityFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.security.inmemory",
        Package = "XiHan.Framework.Security.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides =
        [
            new CapabilityDeclaration(SecurityCapabilities.Authentication, SecurityCapabilities.Version),
            new CapabilityDeclaration(SecurityCapabilities.Authorization, SecurityCapabilities.Version),
        ],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline =
            [
                new PipelineContribution(typeof(AuthenticationMiddleware).FullName!, PipelineStage.Authenticate),
                new PipelineContribution(typeof(AuthorizationMiddleware).FullName!, PipelineStage.Authorize),
            ],
            Config = new ConfigContribution("XiHan:Security"),
            Health = ["security.inmemory.capacity"],
        },
        Entry = typeof(InMemorySecurityFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.TryAddSingleton(SecurityOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton(InMemorySecurityOptions.FromConfiguration(context.Configuration));

        if (context.Node.IsSelectedFor(SecurityCapabilities.Authentication))
        {
            context.Services.AddSingleton<InMemoryPrincipalStore>();
            context.Services.AddSingleton<IAuthenticationHandler, ApiKeyAuthenticationHandler>();
            context.AddMiddleware<AuthenticationMiddleware>(PipelineStage.Authenticate);
        }

        if (context.Node.IsSelectedFor(SecurityCapabilities.Authorization))
        {
            context.Services.AddSingleton<InMemoryPermissionGrantStore>();
            context.Services.TryAddSingleton<IPermissionGrantStore>(static sp => sp.GetRequiredService<InMemoryPermissionGrantStore>());
            context.Services.TryAddSingleton<IPermissionChecker, PermissionChecker>();
            context.Services.AddSingleton<IPolicy, AuthenticatedPolicy>();
            context.Services.TryAddSingleton<PolicyCatalog>();
            context.AddMiddleware<AuthorizationMiddleware>(PipelineStage.Authorize);
        }
    }
}
