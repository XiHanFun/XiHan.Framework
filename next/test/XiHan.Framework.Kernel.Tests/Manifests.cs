// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests;

/// <summary>
/// 测试用的清单工厂。
/// </summary>
internal static class Manifests
{
    /// <summary>
    /// 创建一份最小合法清单：固定版本 5.0.0、原语层版本 1、稳定级别，其余按参数填充。
    /// </summary>
    /// <param name="id">清单 ID。</param>
    /// <param name="provides">提供的能力，<c>id@version</c> 形式。</param>
    /// <param name="requires">依赖的能力，<c>id@version[?]</c> 形式。</param>
    /// <param name="role">角色。</param>
    /// <param name="kind">种类。</param>
    /// <param name="bindings">需要的绑定。</param>
    public static Manifest Create(
        string id,
        string[]? provides = null,
        string[]? requires = null,
        ManifestRole role = ManifestRole.Provider,
        ManifestKind kind = ManifestKind.Capability,
        Binding[]? bindings = null) => new()
    {
        Id = id,
        Version = "5.0.0",
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = kind,
        Role = role,
        Provides = (provides ?? []).Select(CapabilityDeclaration.Parse).ToList(),
        Requires = (requires ?? []).Select(CapabilityRequirement.Parse).ToList(),
        Bindings = bindings ?? [],
    };
}
