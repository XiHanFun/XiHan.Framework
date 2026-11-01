// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 只登记权限码的测试特性。
/// </summary>
/// <remarks>
/// 与登记操作的特性分开装配，验证权限码在装配完成时核对，而不是登记操作的那一刻。
/// </remarks>
/// <param name="manifestId">清单 ID。</param>
/// <param name="codes">登记的权限码。</param>
internal sealed class PermissionsFeature(string manifestId, params string[] codes) : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = manifestId,
        Package = "XiHan.Framework.Kernel.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(PermissionsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        foreach (var code in codes)
        {
            context.AddPermission(new PermissionDefinition(code, code));
        }
    }
}
