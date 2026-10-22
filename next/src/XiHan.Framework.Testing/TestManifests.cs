// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Testing;

/// <summary>
/// 用能力 ID 文本快速构造清单。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class TestManifests
{
    /// <summary>
    /// 创建清单。
    /// </summary>
    /// <param name="id">清单 ID。</param>
    /// <param name="provides">提供的能力，<c>id@version</c>。</param>
    /// <param name="requires">依赖的能力，<c>id@[^]version[?]</c>。</param>
    /// <param name="role">角色。</param>
    /// <param name="kind">种类。</param>
    /// <param name="bindings">绑定。</param>
    public static Manifest Create(
        string id,
        IEnumerable<string>? provides = null,
        IEnumerable<string>? requires = null,
        ManifestRole role = ManifestRole.Provider,
        ManifestKind kind = ManifestKind.Capability,
        IEnumerable<Binding>? bindings = null) => new()
    {
        Id = id,
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = kind,
        Role = role,
        Provides = (provides ?? []).Select(CapabilityDeclaration.Parse).ToList(),
        Requires = (requires ?? []).Select(CapabilityRequirement.Parse).ToList(),
        Bindings = (bindings ?? []).ToList(),
    };
}
