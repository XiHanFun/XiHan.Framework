// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Architecture.Tests.ProjectGraph;

/// <summary>
/// 按分层规则校验项目依赖图
/// </summary>
internal static class ProjectDependencyGraphValidator
{
    private const string AbstractionsSuffix = ".Abstractions";

    /// <summary>
    /// 校验依赖图
    /// </summary>
    /// <remarks>
    /// 只校验源码项目发出的引用：同层与向下引用合法，向上引用须登记例外；
    /// 源码项目只能引用源码项目；契约包不得引用自身或兄弟实现包；
    /// 例外清单中不再是向上引用的条目报告为过期；循环依赖一律报告，例外清单不能豁免。
    /// </remarks>
    /// <param name="graph">依赖图</param>
    /// <param name="policy">例外清单</param>
    /// <returns>违规列表，没有违规时为空</returns>
    public static IReadOnlyList<ArchitectureViolation> Validate(ProjectDependencyGraph graph, DependencyPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(policy);

        var violations = new List<ArchitectureViolation>();

        var unregistered = graph.Projects.Values
            .Where(item => item.Area == ProjectArea.Source && item.Layer is null)
            .OrderBy(item => item.Name, StringComparer.Ordinal);
        foreach (var project in unregistered)
        {
            violations.Add(new ArchitectureViolation(
                ArchitectureViolationKind.UnregisteredLayer,
                $"{Describe(project)} 未登记到 slnx 的 /1.src/<序号>.<层名>/ 分层目录，请在所属层的目录下登记。"));
        }

        var orderedEdges = graph.Edges
            .OrderBy(item => item.From, StringComparer.Ordinal)
            .ThenBy(item => item.To, StringComparer.Ordinal);
        foreach (var edge in orderedEdges)
        {
            if (!graph.Projects.TryGetValue(edge.From, out var from) || from.Area != ProjectArea.Source)
            {
                continue;
            }

            if (!graph.Projects.TryGetValue(edge.To, out var to))
            {
                violations.Add(new ArchitectureViolation(
                    ArchitectureViolationKind.UnknownProject,
                    $"{Describe(from)} 引用了依赖图中不存在的项目 {edge.To}，请检查 ProjectReference 路径。"));
                continue;
            }

            if (to.Area != ProjectArea.Source)
            {
                violations.Add(new ArchitectureViolation(
                    ArchitectureViolationKind.SourceReferencesNonSource,
                    $"{Describe(from)} 引用了非源码项目 {edge.To}（{to.Area}），源码项目只能引用 framework/src 下的项目。"));
                continue;
            }

            if (IsAbstractionsToOwnImplementation(edge))
            {
                violations.Add(new ArchitectureViolation(
                    ArchitectureViolationKind.AbstractionsReferencesImplementation,
                    $"{Describe(from)} 引用了自身或兄弟实现包 {edge.To}，请把所需类型移入契约包或让实现包引用契约包。"));
            }

            if (IsUpward(from, to) && !policy.Allows(edge))
            {
                violations.Add(new ArchitectureViolation(
                    ArchitectureViolationKind.UpwardReference,
                    $"{Describe(from)}（第 {from.Layer!.Rank} 层 {from.Layer.FolderName}）引用了上层的 {edge.To}（第 {to.Layer!.Rank} 层 {to.Layer.FolderName}），"
                    + $"请把所需契约下沉到第 {from.Layer.Rank} 层或以下，或在 FrameworkDependencyPolicy 的例外清单登记理由。"));
            }
        }

        foreach (var exception in policy.Exceptions)
        {
            var stillUpward = graph.Edges.Contains(new ProjectEdge(exception.From, exception.To))
                && graph.Projects.TryGetValue(exception.From, out var from)
                && graph.Projects.TryGetValue(exception.To, out var to)
                && IsUpward(from, to);

            if (!stillUpward)
            {
                violations.Add(new ArchitectureViolation(
                    ArchitectureViolationKind.StaleException,
                    $"依赖例外 {exception.From} → {exception.To} 的引用已不存在或已不是向上引用，请从例外清单删除。"));
            }
        }

        foreach (var cycle in FindCycles(graph))
        {
            violations.Add(new ArchitectureViolation(
                ArchitectureViolationKind.Cycle,
                $"检测到循环依赖：{string.Join(" → ", cycle)}；例外清单不能豁免循环依赖；请移除环上的一条引用或把共用类型下沉到更低层。"));
        }

        return violations;
    }

    private static string Describe(ProjectNode project)
    {
        return project.RelativePath is null
            ? project.Name
            : $"{project.Name}（framework/{project.RelativePath}）";
    }

    private static bool IsUpward(ProjectNode from, ProjectNode to)
    {
        return from.Layer is not null && to.Layer is not null && to.Layer.Rank > from.Layer.Rank;
    }

    private static bool IsAbstractionsToOwnImplementation(ProjectEdge edge)
    {
        if (!edge.From.EndsWith(AbstractionsSuffix, StringComparison.Ordinal)
            || edge.To.EndsWith(AbstractionsSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var baseName = edge.From[..^AbstractionsSuffix.Length];
        return string.Equals(baseName, edge.To, StringComparison.Ordinal)
            || edge.To.StartsWith(baseName + ".", StringComparison.Ordinal);
    }

    private static bool IsSource(ProjectDependencyGraph graph, string name)
    {
        return graph.Projects.TryGetValue(name, out var project) && project.Area == ProjectArea.Source;
    }

    private static List<List<string>> FindCycles(ProjectDependencyGraph graph)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            if (!IsSource(graph, edge.From) || !IsSource(graph, edge.To))
            {
                continue;
            }

            if (!adjacency.TryGetValue(edge.From, out var targets))
            {
                targets = [];
                adjacency[edge.From] = targets;
            }

            targets.Add(edge.To);
        }

        foreach (var targets in adjacency.Values)
        {
            targets.Sort(StringComparer.Ordinal);
        }

        var cycles = new List<List<string>>();
        foreach (var component in FindStronglyConnectedComponents(adjacency))
        {
            var start = component.Min(StringComparer.Ordinal)!;
            var hasSelfLoop = adjacency.TryGetValue(start, out var startTargets) && startTargets.Contains(start);
            if (component.Count == 1 && !hasSelfLoop)
            {
                continue;
            }

            cycles.Add(FindShortestCycle(adjacency, component, start));
        }

        return [.. cycles.OrderBy(item => item[0], StringComparer.Ordinal)];
    }

    private static List<HashSet<string>> FindStronglyConnectedComponents(Dictionary<string, List<string>> adjacency)
    {
        var nextIndex = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<HashSet<string>>();

        var nodes = adjacency.Keys
            .Concat(adjacency.Values.SelectMany(item => item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        foreach (var node in nodes)
        {
            if (!indices.ContainsKey(node))
            {
                Visit(node);
            }
        }

        return components;

        void Visit(string node)
        {
            indices[node] = nextIndex;
            lowLinks[node] = nextIndex;
            nextIndex++;
            stack.Push(node);
            onStack.Add(node);

            if (adjacency.TryGetValue(node, out var targets))
            {
                foreach (var target in targets)
                {
                    if (!indices.ContainsKey(target))
                    {
                        Visit(target);
                        lowLinks[node] = Math.Min(lowLinks[node], lowLinks[target]);
                    }
                    else if (onStack.Contains(target))
                    {
                        lowLinks[node] = Math.Min(lowLinks[node], indices[target]);
                    }
                }
            }

            if (lowLinks[node] != indices[node])
            {
                return;
            }

            var component = new HashSet<string>(StringComparer.Ordinal);
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (!string.Equals(member, node, StringComparison.Ordinal));

            components.Add(component);
        }
    }

    private static List<string> FindShortestCycle(
        Dictionary<string, List<string>> adjacency,
        HashSet<string> component,
        string start)
    {
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                if (!component.Contains(target))
                {
                    continue;
                }

                if (string.Equals(target, start, StringComparison.Ordinal))
                {
                    var path = new List<string> { start };
                    for (var node = current; !string.Equals(node, start, StringComparison.Ordinal); node = parents[node])
                    {
                        path.Insert(1, node);
                    }

                    path.Add(start);
                    return path;
                }

                if (visited.Add(target))
                {
                    parents[target] = current;
                    queue.Enqueue(target);
                }
            }
        }

        return [start, start];
    }
}
