// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests;

internal static class Manifests
{
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
