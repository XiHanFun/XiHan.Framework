// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// 能力图谱求解（RFC-0002）：按能力 ID 与接口版本把每个 <c>requires</c> 绑定到唯一提供者，拓扑排序，报错可解释。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class CapabilityResolver
{
    /// <summary>
    /// 求解。
    /// </summary>
    /// <param name="manifests">参与装配的全部清单，包含内核清单。</param>
    /// <param name="providerSelections">显式指定的提供者：能力 ID → 清单 ID。</param>
    /// <exception cref="FrameworkException">
    /// <see cref="KernelErrors.DuplicateManifest"/>、<see cref="KernelErrors.MissingCapability"/>、<see cref="KernelErrors.CapabilityVersionConflict"/>、
    /// <see cref="KernelErrors.AmbiguousProvider"/>、<see cref="KernelErrors.CircularCapability"/>。
    /// </exception>
    public static ResolvedGraph Resolve(IReadOnlyList<Manifest> manifests, IReadOnlyDictionary<string, string>? providerSelections = null)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        providerSelections ??= new Dictionary<string, string>(StringComparer.Ordinal);

        var nodes = new Dictionary<string, ResolvedNode>(StringComparer.Ordinal);
        foreach (var manifest in manifests)
        {
            if (!nodes.TryAdd(manifest.Id, new ResolvedNode(manifest)))
            {
                throw new FrameworkException(KernelErrors.DuplicateManifest, $"清单 ID '{manifest.Id}' 重复。");
            }
        }

        var providers = new Dictionary<string, ResolvedNode>(StringComparer.Ordinal);
        foreach (var node in nodes.Values)
        {
            foreach (var requirement in node.Manifest.Requires)
            {
                var provider = Select(node, requirement, nodes.Values, providerSelections);
                if (provider is null)
                {
                    node.AddMissingOptional(requirement);
                    continue;
                }

                node.AddDependency(provider);
                providers.TryAdd(requirement.Id, provider);
            }
        }

        // 没有被任何人依赖、但声明了能力的节点，也登记为其能力的提供者，便于查询。
        foreach (var node in nodes.Values)
        {
            foreach (var declaration in node.Manifest.Provides)
            {
                providers.TryAdd(declaration.Id, node);
            }
        }

        var ordered = Sort(manifests.Select(m => nodes[m.Id]).ToList());
        return new ResolvedGraph(ordered, providers);
    }

    private static ResolvedNode? Select(ResolvedNode requester, CapabilityRequirement requirement, IEnumerable<ResolvedNode> all, IReadOnlyDictionary<string, string> selections)
    {
        var sameId = all.Where(n => n.Manifest.Provides.Any(p => string.Equals(p.Id, requirement.Id, StringComparison.Ordinal))).ToList();
        var candidates = sameId.Where(n => n.Manifest.Provides.Any(requirement.IsSatisfiedBy)).ToList();

        if (selections.TryGetValue(requirement.Id, out var selectedId))
        {
            var selected = candidates.FirstOrDefault(n => string.Equals(n.Manifest.Id, selectedId, StringComparison.Ordinal));
            if (selected is not null)
            {
                return selected;
            }

            if (!requirement.Optional)
            {
                throw new FrameworkException(
                    KernelErrors.MissingCapability,
                    $"配置指定 {requirement.Id} 由 '{selectedId}' 提供，但它不存在或不提供 {requirement}。候选：{Describe(candidates)}。");
            }

            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        if (candidates.Count == 0)
        {
            if (requirement.Optional)
            {
                return null;
            }

            if (sameId.Count > 0)
            {
                var offered = string.Join("、", sameId.Select(n => $"{n.Manifest.Id} 提供 {n.Manifest.Provides.First(p => p.Id == requirement.Id)}"));
                throw new FrameworkException(KernelErrors.CapabilityVersionConflict, $"{requester.Manifest.Id} 需要 {requirement}，但 {offered}。");
            }

            throw new FrameworkException(KernelErrors.MissingCapability, $"{requester.Manifest.Id} 需要 {requirement}，没有任何清单提供它。");
        }

        var defaults = candidates.Where(static n => n.Manifest.Role == ManifestRole.Default).ToList();
        if (defaults.Count == 1)
        {
            return defaults[0];
        }

        throw new FrameworkException(
            KernelErrors.AmbiguousProvider,
            $"{requester.Manifest.Id} 需要 {requirement}，有多个候选且未指定：{Describe(candidates)}。用配置 XiHan:Providers:{requirement.Id} 指定清单 ID。");
    }

    private static List<ResolvedNode> Sort(List<ResolvedNode> nodes)
    {
        var ordered = new List<ResolvedNode>(nodes.Count);
        var visited = new HashSet<ResolvedNode>();
        var path = new List<ResolvedNode>();

        foreach (var node in nodes)
        {
            Visit(node, visited, path, ordered);
        }

        return ordered;
    }

    private static void Visit(ResolvedNode node, HashSet<ResolvedNode> visited, List<ResolvedNode> path, List<ResolvedNode> ordered)
    {
        if (visited.Contains(node))
        {
            return;
        }

        var cycleStart = path.IndexOf(node);
        if (cycleStart >= 0)
        {
            var cycle = path.Skip(cycleStart).Append(node).Select(static n => n.Manifest.Id);
            throw new FrameworkException(KernelErrors.CircularCapability, $"能力依赖成环：{string.Join(" -> ", cycle)}。");
        }

        path.Add(node);
        foreach (var dependency in node.Dependencies)
        {
            Visit(dependency, visited, path, ordered);
        }

        path.RemoveAt(path.Count - 1);
        visited.Add(node);
        ordered.Add(node);
    }

    private static string Describe(IEnumerable<ResolvedNode> nodes)
    {
        var ids = nodes.Select(static n => n.Manifest.Id).ToList();
        return ids.Count == 0 ? "无" : string.Join("、", ids);
    }
}
