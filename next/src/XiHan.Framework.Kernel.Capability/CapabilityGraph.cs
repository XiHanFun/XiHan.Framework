// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capability;

/// <summary>
/// 能力依赖图。
/// 支持拓扑排序和循环依赖检测。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public sealed class CapabilityGraph
{
    private readonly Dictionary<string, CapabilityNode> _nodes = [];

    /// <summary>
    /// 添加一个能力到图中。
    /// </summary>
    public CapabilityGraph Add(ICapability capability)
    {
        _nodes[capability.Name] = new CapabilityNode(capability);
        return this;
    }

    /// <summary>
    /// 对所有能力进行拓扑排序。如果存在循环依赖则抛出异常。
    /// </summary>
    public IReadOnlyList<ICapability> Sort()
    {
        var sorted = new List<ICapability>();
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var name in _nodes.Keys)
        {
            if (!visited.Contains(name))
            {
                Visit(name, visited, inStack, sorted);
            }
        }

        return sorted;
    }

    /// <summary>
    /// 验证所有能力的依赖是否满足。
    /// </summary>
    public CapabilityValidationResult Validate()
    {
        var errors = new List<string>();

        foreach (var (name, node) in _nodes)
        {
            foreach (var (dep, _) in node.Capability.Requires)
            {
                if (!_nodes.ContainsKey(dep))
                {
                    errors.Add($"Missing dependency: '{name}' requires '{dep}' which is not registered.");
                }
            }
        }

        var hasCycle = false;
        try
        {
            Sort();
        }
        catch (InvalidOperationException)
        {
            hasCycle = true; errors.Add("Circular dependency detected.");
        }

        return new CapabilityValidationResult(errors.Count == 0 && !hasCycle, errors);
    }

    private void Visit(string name, HashSet<string> visited, HashSet<string> inStack, List<ICapability> sorted)
    {
        if (inStack.Contains(name))
            throw new InvalidOperationException($"Circular dependency detected involving '{name}'.");

        if (visited.Contains(name))
            return;

        if (!_nodes.TryGetValue(name, out var node))
            throw new InvalidOperationException($"Capability '{name}' is not registered.");

        inStack.Add(name);

        foreach (var (depName, _) in node.Capability.Requires)
        {
            Visit(depName, visited, inStack, sorted);
        }

        inStack.Remove(name);
        visited.Add(name);
        sorted.Add(node.Capability);
    }

    private sealed record CapabilityNode(ICapability Capability);
}

/// <summary>
/// 能力验证结果。
/// </summary>
public sealed record CapabilityValidationResult(bool IsValid, IReadOnlyList<string> XiHanErrors);
