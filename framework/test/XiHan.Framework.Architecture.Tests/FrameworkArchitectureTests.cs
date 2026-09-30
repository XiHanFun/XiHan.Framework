// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Architecture.Tests.ProjectGraph;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 本仓库的架构规则测试
/// </summary>
public class FrameworkArchitectureTests
{
    /// <summary>
    /// 仓库全部源码项目的引用符合分层、无环且只引用源码项目
    /// </summary>
    [Fact]
    public void 仓库项目依赖符合分层规则()
    {
        var graph = ProjectGraphLoader.Load(ProjectGraphLoader.FindFrameworkDirectory());

        var violations = ProjectDependencyGraphValidator.Validate(graph, FrameworkDependencyPolicy.Current);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Select(item => item.Message)));
    }

    /// <summary>
    /// 真实依赖图读到了已知的核心引用与分层
    /// </summary>
    [Fact]
    public void 仓库依赖图读到核心引用与分层()
    {
        var graph = ProjectGraphLoader.Load(ProjectGraphLoader.FindFrameworkDirectory());

        Assert.Contains(new ProjectEdge("XiHan.Framework.Core", "XiHan.Framework.Utils"), graph.Edges);
        Assert.Equal(3, graph.Projects["XiHan.Framework.Core"].Layer?.Rank);
        Assert.Equal(7, graph.Projects["XiHan.Framework.Web.Api"].Layer?.Rank);
    }
}
