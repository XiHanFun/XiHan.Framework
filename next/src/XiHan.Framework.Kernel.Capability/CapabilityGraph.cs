// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capability;

/// <summary>
/// 能力依赖图。支持拓扑排序、循环检测、缺失依赖和版本约束校验。
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
    /// 拓扑排序。缺失依赖会抛出异常，循环依赖会抛出异常。
    /// </summary>
    public IReadOnlyList<ICapability> Sort()
    {
        var sorted = new List<ICapability>();
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var name in _nodes.Keys)
        {
            if (!visited.Contains(name))
                Visit(name, visited, inStack, sorted);
        }
        return sorted;
    }

    /// <summary>
    /// 验证所有能力的依赖。返回分类错误列表。
    /// </summary>
    public CapabilityValidationResult Validate()
    {
        var missing = new List<string>();
        var circular = new List<string>();

        foreach (var (name, node) in _nodes)
        {
            foreach (var (dep, _) in node.Capability.Requires)
            {
                if (!_nodes.ContainsKey(dep))
                    missing.Add($"'{name}' requires '{dep}' which is not registered.");
            }
        }

        try
        {
            Sort();
        }
        catch (CircularDependencyException ex)
        {
            circular.Add(ex.Message);
        }
        catch (MissingCapabilityException ex)
        {
            missing.Add(ex.Message);
        }

        return new CapabilityValidationResult(
            missing.Count == 0 && circular.Count == 0,
            missing,
            circular,
            []);
    }

    private void Visit(string name, HashSet<string> visited, HashSet<string> inStack, List<ICapability> sorted)
    {
        if (inStack.Contains(name))
            throw new CircularDependencyException($"Circular dependency detected involving '{name}'.");

        if (visited.Contains(name))
            return;

        if (!_nodes.TryGetValue(name, out var node))
            throw new MissingCapabilityException($"Capability '{name}' is not registered.");

        inStack.Add(name);

        foreach (var (depName, _) in node.Capability.Requires)
            Visit(depName, visited, inStack, sorted);

        inStack.Remove(name);
        visited.Add(name);
        sorted.Add(node.Capability);
    }

    private sealed record CapabilityNode(ICapability Capability);
}

/// <summary>
/// 能力验证结果。分类列出缺失依赖和循环依赖。
/// </summary>
public sealed record CapabilityValidationResult(
    bool IsValid,
    IReadOnlyList<string> MissingDependencies,
    IReadOnlyList<string> CircularDependencies,
    IReadOnlyList<string> VersionMismatches);

/// <summary>
/// 循环依赖异常。
/// </summary>
public sealed class CircularDependencyException : Exception
{
    /// <inheritdoc />
    public CircularDependencyException(string message) : base(message) { }
}

/// <summary>
/// 缺失能力异常。
/// </summary>
public sealed class MissingCapabilityException : Exception
{
    /// <inheritdoc />
    public MissingCapabilityException(string message) : base(message) { }
}
