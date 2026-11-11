// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 通行密钥（WebAuthn）的能力包入口
/// </summary>
/// <remarks>
/// 提供 <c>security.webauthn@1</c>，以 <see cref="WebAuthnCeremonies"/> 单例生成选项与核对响应；配置见 <see cref="WebAuthnOptions"/>，缺少必填项即启动失败
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WebAuthnSecurityFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.security.webauthn",
        Package = "XiHan.Framework.Security.WebAuthn",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(SecurityCapabilities.WebAuthn, SecurityCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution(WebAuthnOptions.ConfigurationSection),
        },
        Entry = typeof(WebAuthnSecurityFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 核对失败的错误码文案在安全子系统的目录里；同名目录只注册一次
        context.Services.AddMessageCatalog(SecurityMessages.Catalog);
        var options = WebAuthnOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        context.Services.AddSingleton(new WebAuthnCeremonies(options));
    }
}
