// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel;

/// <summary>
/// 内核提供的能力 ID。特性在清单的 <c>requires</c> 中引用它们。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class KernelCapabilities
{
    /// <summary>
    /// 应用构建、特性装配、生命周期。
    /// </summary>
    public const string Hosting = "kernel.hosting";

    /// <summary>
    /// 清单解析与能力图谱。
    /// </summary>
    public const string Capabilities = "kernel.capabilities";

    /// <summary>
    /// 执行管道。
    /// </summary>
    public const string Pipeline = "kernel.pipeline";

    /// <summary>
    /// 诊断薄桥与审计接收器。
    /// </summary>
    public const string Diagnostics = "kernel.diagnostics";

    /// <summary>
    /// 安全原语。
    /// </summary>
    public const string Security = "kernel.security";

    /// <summary>
    /// 插件宿主抽象。
    /// </summary>
    public const string Plugins = "kernel.plugins";

    /// <summary>
    /// 当前内核实现的接口版本。
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// 内核自身的清单：所有内核能力的提供者，总是能力图谱的根。
    /// </summary>
    public static Manifest Manifest { get; } = new()
    {
        Id = "xihan.kernel.core",
        Package = "XiHan.Framework.Kernel",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = ManifestKind.Capability,
        Provides =
        [
            new CapabilityDeclaration(Hosting, Version),
            new CapabilityDeclaration(Capabilities, Version),
            new CapabilityDeclaration(Pipeline, Version),
            new CapabilityDeclaration(Diagnostics, Version),
            new CapabilityDeclaration(Security, Version),
            new CapabilityDeclaration(Plugins, Version),
        ],
        Entry = "XiHan.Framework.Kernel.Hosting.App",
    };
}
