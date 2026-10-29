// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// 能力图谱求解（RFC-0002）。
/// </summary>
/// <remarks>
/// 按能力 ID 与接口版本把每个 <c>requires</c> 绑定到唯一提供者，拓扑排序，报错可解释。
/// </remarks>
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
                throw new FrameworkException(KernelErrors.DuplicateManifest, $"Manifest ID '{manifest.Id}' is declared more than once.");
            }
        }

        // 第一遍：为每条 requires 选提供者并建边。同一能力只记录第一次选中的提供者，后续依赖者复用同一结果。
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

        // 没有被任何人依赖的能力也按同一规则选出提供者：显式 Provider 优先，其次唯一的 default，否则要求配置指定。
        foreach (var group in nodes.Values
                     .SelectMany(static n => n.Manifest.Provides.Select(p => (p.Id, Node: n)))
                     .GroupBy(static x => x.Id, StringComparer.Ordinal))
        {
            if (!providers.ContainsKey(group.Key))
            {
                providers[group.Key] = Choose(group.Key, group.Select(static x => x.Node).ToList(), selections: providerSelections, what: $"Capability '{group.Key}'");
            }
        }

        // 把选中结果回填到节点：未被任何能力选中的节点将被宿主视为 shadowed，不装配。
        foreach (var (capabilityId, provider) in providers)
        {
            provider.AddSelected(capabilityId);
        }

        var ordered = Sort(manifests.Select(m => nodes[m.Id]).ToList());
        return new ResolvedGraph(ordered, providers);
    }

    /// <summary>
    /// 为一条 <c>requires</c> 选提供者。
    /// </summary>
    /// <remarks>
    /// 配置指定优先；无配置时按候选数与角色决定；可选依赖缺失返回 <see langword="null"/>。
    /// </remarks>
    /// <param name="requester">声明该依赖的节点，仅用于错误信息。</param>
    /// <param name="requirement">待满足的依赖。</param>
    /// <param name="all">全部节点。</param>
    /// <param name="selections">配置或代码显式指定的提供者。</param>
    /// <returns>选中的提供者；可选依赖无法满足时为 <see langword="null"/>。</returns>
    private static ResolvedNode? Select(ResolvedNode requester, CapabilityRequirement requirement, IEnumerable<ResolvedNode> all, IReadOnlyDictionary<string, string> selections)
    {
        // sameId 用于区分"没人提供"与"有人提供但版本不符"，后者报版本冲突并列出双方版本。
        var sameId = all.Where(n => n.Manifest.Provides.Any(p => string.Equals(p.Id, requirement.Id, StringComparison.Ordinal))).ToList();
        var candidates = sameId.Where(n => n.Manifest.Provides.Any(requirement.IsSatisfiedBy)).ToList();

        if (selections.TryGetValue(requirement.Id, out var selectedId))
        {
            var selected = candidates.FirstOrDefault(n => string.Equals(n.Manifest.Id, selectedId, StringComparison.Ordinal));
            if (selected is not null)
            {
                return selected;
            }

            // 配置指向了不存在或版本不符的清单：必需依赖直接失败，可选依赖按缺失处理。
            if (!requirement.Optional)
            {
                throw new FrameworkException(
                    KernelErrors.MissingCapability,
                    $"Configuration selects '{selectedId}' to provide '{requirement.Id}', but it does not exist or does not provide '{requirement}'. Candidates: {Describe(candidates)}.");
            }

            return null;
        }

        if (candidates.Count == 0)
        {
            if (requirement.Optional)
            {
                return null;
            }

            if (sameId.Count > 0)
            {
                var offered = string.Join(", ", sameId.Select(n => $"'{n.Manifest.Id}' provides '{n.Manifest.Provides.First(p => p.Id == requirement.Id)}'"));
                throw new FrameworkException(KernelErrors.CapabilityVersionConflict, $"'{requester.Manifest.Id}' requires '{requirement}', but {offered}.");
            }

            throw new FrameworkException(KernelErrors.MissingCapability, $"'{requester.Manifest.Id}' requires '{requirement}', but no manifest provides it.");
        }

        return Choose(requirement.Id, candidates, selections: null, what: $"'{requester.Manifest.Id}' requires '{requirement}', which");
    }

    /// <summary>
    /// 在候选里选一个。
    /// </summary>
    /// <remarks>
    /// 配置指定 → 唯一的显式 Provider → 唯一的 default → 否则多候选未指定。
    /// </remarks>
    /// <param name="capabilityId">能力 ID。</param>
    /// <param name="candidates">提供该能力且版本匹配的节点。</param>
    /// <param name="selections">显式指定的提供者；<see langword="null"/> 表示调用方已处理过配置指定。</param>
    /// <param name="what">错误信息的主语，描述是谁需要这个能力。</param>
    private static ResolvedNode Choose(string capabilityId, IReadOnlyList<ResolvedNode> candidates, IReadOnlyDictionary<string, string>? selections, string what)
    {
        if (selections is not null && selections.TryGetValue(capabilityId, out var selectedId))
        {
            var selected = candidates.FirstOrDefault(n => string.Equals(n.Manifest.Id, selectedId, StringComparison.Ordinal));
            if (selected is not null)
            {
                return selected;
            }

            throw new FrameworkException(
                KernelErrors.MissingCapability,
                $"Configuration selects '{selectedId}' to provide '{capabilityId}', but it does not exist or does not provide '{capabilityId}'. Candidates: {Describe(candidates)}.");
        }

        // default 角色只在没有任何显式 Provider 时参与竞争；有显式 Provider 时 default 被顶替。
        var explicitProviders = candidates.Where(static n => n.Manifest.Role != ManifestRole.Default).ToList();
        var pool = explicitProviders.Count > 0 ? explicitProviders : candidates;
        if (pool.Count == 1)
        {
            return pool[0];
        }

        throw new FrameworkException(
            KernelErrors.AmbiguousProvider,
            $"{what} has more than one candidate provider and none is selected: {Describe(pool)}. Specify a manifest ID with the configuration key 'XiHan:Providers:{capabilityId}'.");
    }

    /// <summary>
    /// 对节点做深度优先拓扑排序：被依赖者在前，输入顺序作为同层次的次序，保证结果确定。
    /// </summary>
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

    /// <summary>
    /// 深度优先访问一个节点。
    /// </summary>
    /// <remarks>
    /// 先输出其全部依赖，再输出自身；沿当前路径再次遇到同一节点即为环。
    /// </remarks>
    /// <param name="node">当前节点。</param>
    /// <param name="visited">已完成输出的节点。</param>
    /// <param name="path">当前递归路径，用于检测环并给出可读的环路。</param>
    /// <param name="ordered">输出序列。</param>
    private static void Visit(ResolvedNode node, HashSet<ResolvedNode> visited, List<ResolvedNode> path, List<ResolvedNode> ordered)
    {
        if (visited.Contains(node))
        {
            return;
        }

        // 节点出现在当前路径上说明依赖成环；从首次出现处截取路径，报错时打印完整环路。
        var cycleStart = path.IndexOf(node);
        if (cycleStart >= 0)
        {
            var cycle = path.Skip(cycleStart).Append(node).Select(static n => n.Manifest.Id);
            throw new FrameworkException(KernelErrors.CircularCapability, $"Capability dependencies form a cycle: {string.Join(" -> ", cycle)}.");
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

    /// <summary>
    /// 把节点列表格式化为错误信息里的清单 ID 列表。
    /// </summary>
    /// <remarks>
    /// 空列表输出 <c>none</c>。
    /// </remarks>
    private static string Describe(IEnumerable<ResolvedNode> nodes)
    {
        var ids = nodes.Select(static n => $"'{n.Manifest.Id}'").ToList();
        return ids.Count == 0 ? "none" : string.Join(", ", ids);
    }
}
