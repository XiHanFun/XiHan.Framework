// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// 标注特性入口类型的能力声明。源生成器据此生成 <see cref="Manifest"/> 并实现入口类型的 <c>Manifest</c> 属性；
/// 内核运行期不读取本特性（零反射）。
/// </summary>
/// <param name="id">清单 ID，<c>xihan.&lt;领域&gt;.&lt;名称&gt;</c>。</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CapabilityAttribute(string id) : Attribute
{
    /// <summary>
    /// 清单 ID。
    /// </summary>
    public string Id { get; } = ManifestId.Validate(id);

    /// <summary>
    /// 提供的能力，<c>id@version</c> 形式。
    /// </summary>
    public string[] Provides { get; init; } = [];

    /// <summary>
    /// 依赖的能力，<c>id@[^]version[?]</c> 形式。
    /// </summary>
    public string[] Requires { get; init; } = [];

    /// <summary>
    /// 种类。
    /// </summary>
    public ManifestKind Kind { get; init; } = ManifestKind.Capability;

    /// <summary>
    /// 角色。
    /// </summary>
    public ManifestRole Role { get; init; } = ManifestRole.Provider;

    /// <summary>
    /// 稳定性。
    /// </summary>
    public Stability Stability { get; init; } = Stability.Stable;
}
