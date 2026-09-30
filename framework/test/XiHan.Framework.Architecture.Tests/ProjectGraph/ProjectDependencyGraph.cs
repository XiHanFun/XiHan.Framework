// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Architecture.Tests.ProjectGraph;

/// <summary>
/// 项目依赖图
/// </summary>
internal sealed class ProjectDependencyGraph
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="projects">项目集合，同名项目以后出现的为准</param>
    /// <param name="edges">依赖边集合，重复边只保留一条</param>
    public ProjectDependencyGraph(IEnumerable<ProjectNode> projects, IEnumerable<ProjectEdge> edges)
    {
        var map = new Dictionary<string, ProjectNode>(StringComparer.Ordinal);
        foreach (var project in projects)
        {
            map[project.Name] = project;
        }

        Projects = map;
        Edges = [.. edges.Distinct()];
    }

    /// <summary>
    /// 按项目名索引的项目
    /// </summary>
    public IReadOnlyDictionary<string, ProjectNode> Projects { get; }

    /// <summary>
    /// 依赖边
    /// </summary>
    public IReadOnlyList<ProjectEdge> Edges { get; }
}
