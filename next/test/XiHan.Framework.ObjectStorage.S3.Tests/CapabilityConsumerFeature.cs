// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 只在清单里依赖一项对象存储能力的特性。
/// </summary>
/// <remarks>
/// 模拟应用的文件模块：依赖使该能力成为被要求的选中（RFC-0002 解析规则 4），本身不注册任何服务。
/// </remarks>
/// <param name="capabilityId">依赖的能力 ID。</param>
internal sealed class CapabilityConsumerFeature(string capabilityId) : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.objectstorage.consumer",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(capabilityId, 1),
        ],
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context) => ArgumentNullException.ThrowIfNull(context);
}
