// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// 已解析图中的一个节点：清单、它所依赖的节点、缺失的可选依赖。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ResolvedNode
{
    private readonly List<ResolvedNode> _dependencies = [];
    private readonly List<CapabilityRequirement> _missingOptional = [];

    internal ResolvedNode(Manifest manifest) => Manifest = manifest;

    /// <summary>
    /// 清单。
    /// </summary>
    public Manifest Manifest { get; }

    /// <summary>
    /// 本节点依赖的节点（按清单 <c>requires</c> 顺序）。
    /// </summary>
    public IReadOnlyList<ResolvedNode> Dependencies => _dependencies;

    /// <summary>
    /// 缺失的可选依赖；非空时对应特性启动后进入 <see cref="LifecycleState.Degraded"/>。
    /// </summary>
    public IReadOnlyList<CapabilityRequirement> MissingOptional => _missingOptional;

    /// <summary>
    /// 是否因可选依赖缺失而降级。
    /// </summary>
    public bool IsDegraded => _missingOptional.Count > 0;

    internal void AddDependency(ResolvedNode node) => _dependencies.Add(node);

    internal void AddMissingOptional(CapabilityRequirement requirement) => _missingOptional.Add(requirement);

    /// <inheritdoc />
    public override string ToString() => Manifest.Id;
}

/// <summary>
/// 能力解析的结果：拓扑有序的节点与每个能力选中的提供者。<c>xihan graph</c> 与启动日志打印同一份。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ResolvedGraph
{
    private readonly Dictionary<string, ResolvedNode> _byId;

    internal ResolvedGraph(IReadOnlyList<ResolvedNode> nodes, IReadOnlyDictionary<string, ResolvedNode> providers)
    {
        Nodes = nodes;
        Providers = providers;
        _byId = nodes.ToDictionary(static n => n.Manifest.Id, StringComparer.Ordinal);
    }

    /// <summary>
    /// 拓扑有序的节点，被依赖者在前，内核总在最前。
    /// </summary>
    public IReadOnlyList<ResolvedNode> Nodes { get; }

    /// <summary>
    /// 每个能力 ID 选中的提供者。
    /// </summary>
    public IReadOnlyDictionary<string, ResolvedNode> Providers { get; }

    /// <summary>
    /// 按清单 ID 取节点。
    /// </summary>
    public ResolvedNode this[string manifestId]
        => _byId.TryGetValue(manifestId, out var node) ? node : throw new KeyNotFoundException($"已解析图中没有清单 '{manifestId}'。");

    /// <summary>
    /// 是否包含清单。
    /// </summary>
    public bool Contains(string manifestId) => _byId.ContainsKey(manifestId);

    /// <summary>
    /// 打印已解析图：一个节点一段，列出提供与依赖的去向。
    /// </summary>
    public string Describe()
    {
        var builder = new StringBuilder();
        foreach (var node in Nodes)
        {
            builder.Append(node.Manifest.Id).Append(' ').Append(node.Manifest.Version);
            if (node.Manifest.Role == ManifestRole.Default)
            {
                builder.Append(" (default)");
            }

            builder.Append('\n');
            foreach (var provided in node.Manifest.Provides)
            {
                builder.Append("  provides ").Append(provided).Append('\n');
            }

            foreach (var requirement in node.Manifest.Requires)
            {
                builder.Append("  requires ").Append(requirement).Append(" -> ");
                var dependency = node.Dependencies.FirstOrDefault(d => d.Manifest.Provides.Any(requirement.IsSatisfiedBy));
                builder.Append(dependency is null ? "(missing, optional)" : dependency.Manifest.Id).Append('\n');
            }
        }

        return builder.ToString();
    }
}
