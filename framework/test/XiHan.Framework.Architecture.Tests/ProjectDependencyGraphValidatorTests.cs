// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Architecture.Tests.ProjectGraph;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 依赖图校验规则测试
/// </summary>
public class ProjectDependencyGraphValidatorTests
{
    private static readonly DependencyPolicy NoExceptions = new([]);

    /// <summary>
    /// 同层与向下引用不报告
    /// </summary>
    [Fact]
    public void 同层与下层引用不报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Core", 3), Source("Utils", 1), Source("Timing", 3)],
            [new ProjectEdge("Core", "Utils"), new ProjectEdge("Timing", "Core")]);

        Assert.Empty(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));
    }

    /// <summary>
    /// 向上引用报告起点、终点与所在层
    /// </summary>
    [Fact]
    public void 向上层引用报告起点与终点()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Core", 3), Source("Web", 7)],
            [new ProjectEdge("Core", "Web")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.UpwardReference, violation.Kind);
        Assert.Contains("Core（第 3 层 Layer3）", violation.Message);
        Assert.Contains("Web（第 7 层 Layer7）", violation.Message);
    }

    /// <summary>
    /// 例外清单登记的向上引用不报告
    /// </summary>
    [Fact]
    public void 例外清单登记的向上引用不报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Core", 3), Source("Web", 7)],
            [new ProjectEdge("Core", "Web")]);
        var policy = new DependencyPolicy([new DependencyException("Core", "Web", "测试")]);

        Assert.Empty(ProjectDependencyGraphValidator.Validate(graph, policy));
    }

    /// <summary>
    /// 三个项目构成的环报告完整路径
    /// </summary>
    [Fact]
    public void 三节点环报告完整路径()
    {
        var graph = new ProjectDependencyGraph(
            [Source("A", 6), Source("B", 6), Source("C", 6)],
            [new ProjectEdge("A", "B"), new ProjectEdge("B", "C"), new ProjectEdge("C", "A")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.Cycle, violation.Kind);
        Assert.Contains("A → B → C → A", violation.Message);
    }

    /// <summary>
    /// 例外清单放行了环上的向上引用，环仍被报告
    /// </summary>
    [Fact]
    public void 例外清单不能隐藏环()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Low", 1), Source("High", 2)],
            [new ProjectEdge("Low", "High"), new ProjectEdge("High", "Low")]);
        var policy = new DependencyPolicy([new DependencyException("Low", "High", "测试")]);

        var violations = ProjectDependencyGraphValidator.Validate(graph, policy);

        Assert.Contains(violations, item => item.Kind == ArchitectureViolationKind.Cycle && item.Message.Contains("High → Low → High"));
        Assert.DoesNotContain(violations, item => item.Kind == ArchitectureViolationKind.UpwardReference);
    }

    /// <summary>
    /// 源码项目引用测试项目时报告
    /// </summary>
    [Fact]
    public void 源码项目引用测试项目报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Core", 3), new ProjectNode("Core.Tests", ProjectArea.Test, null)],
            [new ProjectEdge("Core", "Core.Tests")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.SourceReferencesNonSource, violation.Kind);
        Assert.Contains("Core.Tests", violation.Message);
    }

    /// <summary>
    /// 测试项目引用源码项目不受分层约束
    /// </summary>
    [Fact]
    public void 测试项目引用源码项目不报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Web", 7), new ProjectNode("Core.Tests", ProjectArea.Test, null)],
            [new ProjectEdge("Core.Tests", "Web")]);

        Assert.Empty(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));
    }

    /// <summary>
    /// 契约包引用自身实现包时报告
    /// </summary>
    [Fact]
    public void 契约包引用自身实现包报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("EventBus.Abstractions", 6), Source("EventBus", 6)],
            [new ProjectEdge("EventBus.Abstractions", "EventBus")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.AbstractionsReferencesImplementation, violation.Kind);
    }

    /// <summary>
    /// 未登记分层目录的源码项目报告
    /// </summary>
    [Fact]
    public void 未登记分层的源码项目报告()
    {
        var graph = new ProjectDependencyGraph([new ProjectNode("Orphan", ProjectArea.Source, null)], []);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.UnregisteredLayer, violation.Kind);
        Assert.Contains("Orphan", violation.Message);
    }

    /// <summary>
    /// 例外清单中引用已不存在的条目报告过期
    /// </summary>
    [Fact]
    public void 例外清单中已不存在的引用报告过期()
    {
        var graph = new ProjectDependencyGraph([Source("Core", 3), Source("Web", 7)], []);
        var policy = new DependencyPolicy([new DependencyException("Core", "Web", "测试")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, policy));

        Assert.Equal(ArchitectureViolationKind.StaleException, violation.Kind);
    }

    /// <summary>
    /// 引用依赖图中不存在的项目时报告
    /// </summary>
    [Fact]
    public void 引用不存在的项目报告()
    {
        var graph = new ProjectDependencyGraph([Source("Core", 3)], [new ProjectEdge("Core", "Missing")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.UnknownProject, violation.Kind);
    }

    /// <summary>
    /// 多条回边的强连通分量只报告一条含最短环路径的违规
    /// </summary>
    [Fact]
    public void 多回边的环只报告最短路径()
    {
        var graph = new ProjectDependencyGraph(
            [Source("A", 6), Source("B", 6), Source("C", 6)],
            [new ProjectEdge("A", "B"), new ProjectEdge("B", "C"), new ProjectEdge("C", "A"), new ProjectEdge("A", "C")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.Cycle, violation.Kind);
        Assert.Contains("A → C → A", violation.Message);
    }

    /// <summary>
    /// 互不相连的两个环各报告一条
    /// </summary>
    [Fact]
    public void 互不相连的两个环各报告一条()
    {
        var graph = new ProjectDependencyGraph(
            [Source("A", 6), Source("B", 6), Source("C", 6), Source("D", 6)],
            [new ProjectEdge("A", "B"), new ProjectEdge("B", "A"), new ProjectEdge("C", "D"), new ProjectEdge("D", "C")]);

        var violations = ProjectDependencyGraphValidator.Validate(graph, NoExceptions);

        Assert.Equal(2, violations.Count);
        Assert.All(violations, item => Assert.Equal(ArchitectureViolationKind.Cycle, item.Kind));
        Assert.Contains(violations, item => item.Message.Contains("A → B → A"));
        Assert.Contains(violations, item => item.Message.Contains("C → D → C"));
    }

    /// <summary>
    /// 项目引用自身时按环报告
    /// </summary>
    [Fact]
    public void 自环报告为环()
    {
        var graph = new ProjectDependencyGraph([Source("A", 6)], [new ProjectEdge("A", "A")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.Cycle, violation.Kind);
        Assert.Contains("A → A", violation.Message);
    }

    /// <summary>
    /// 菱形依赖不是环
    /// </summary>
    [Fact]
    public void 菱形依赖不报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("A", 6), Source("B", 6), Source("C", 6), Source("D", 6)],
            [new ProjectEdge("A", "B"), new ProjectEdge("A", "C"), new ProjectEdge("B", "D"), new ProjectEdge("C", "D")]);

        Assert.Empty(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));
    }

    /// <summary>
    /// 例外清单登记了同层引用时报告过期
    /// </summary>
    [Fact]
    public void 例外清单登记同层引用报告过期()
    {
        var graph = new ProjectDependencyGraph([Source("A", 6), Source("B", 6)], [new ProjectEdge("A", "B")]);
        var policy = new DependencyPolicy([new DependencyException("A", "B", "测试")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, policy));

        Assert.Equal(ArchitectureViolationKind.StaleException, violation.Kind);
    }

    /// <summary>
    /// 契约包引用兄弟实现包时报告
    /// </summary>
    [Fact]
    public void 契约包引用兄弟实现包报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("EventBus.Abstractions", 6), Source("EventBus.Kafka", 6)],
            [new ProjectEdge("EventBus.Abstractions", "EventBus.Kafka")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.AbstractionsReferencesImplementation, violation.Kind);
    }

    /// <summary>
    /// 契约包引用其他契约包不报告
    /// </summary>
    [Fact]
    public void 契约包引用其他契约包不报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("EventBus.Abstractions", 6), Source("MultiTenancy.Abstractions", 6)],
            [new ProjectEdge("EventBus.Abstractions", "MultiTenancy.Abstractions")]);

        Assert.Empty(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));
    }

    /// <summary>
    /// 向上引用的说明指向例外清单所在类型
    /// </summary>
    [Fact]
    public void 向上引用说明指向例外清单类型()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Core", 3), Source("Web", 7)],
            [new ProjectEdge("Core", "Web")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Contains("FrameworkDependencyPolicy 的例外清单登记理由", violation.Message);
    }

    /// <summary>
    /// 环的说明给出修正方向
    /// </summary>
    [Fact]
    public void 环的说明给出修正方向()
    {
        var graph = new ProjectDependencyGraph(
            [Source("A", 6), Source("B", 6)],
            [new ProjectEdge("A", "B"), new ProjectEdge("B", "A")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.EndsWith("请移除环上的一条引用或把共用类型下沉到更低层。", violation.Message);
    }

    /// <summary>
    /// 契约包引用基础包或名称仅以实现包名为前缀的包不报告
    /// </summary>
    [Fact]
    public void 契约包引用基础包与同前缀无点号的包不报告()
    {
        var graph = new ProjectDependencyGraph(
            [Source("EventBus.Abstractions", 6), Source("Core", 3), Source("EventBusX", 6)],
            [new ProjectEdge("EventBus.Abstractions", "Core"), new ProjectEdge("EventBus.Abstractions", "EventBusX")]);

        Assert.Empty(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));
    }

    /// <summary>
    /// 项目带相对路径时向上引用的说明在起点项目名后给出路径
    /// </summary>
    [Fact]
    public void 向上引用说明带起点路径()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Core", 3, "src/Core/Core.csproj"), Source("Web", 7)],
            [new ProjectEdge("Core", "Web")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Contains("Core（framework/src/Core/Core.csproj）（第 3 层 Layer3）", violation.Message);
    }

    /// <summary>
    /// 项目带相对路径时未登记分层的说明给出路径
    /// </summary>
    [Fact]
    public void 未登记分层说明带路径()
    {
        var graph = new ProjectDependencyGraph([new ProjectNode("Orphan", ProjectArea.Source, null, "src/Orphan/Orphan.csproj")], []);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Contains("Orphan（framework/src/Orphan/Orphan.csproj）", violation.Message);
    }

    /// <summary>
    /// 项目带相对路径时源码引用非源码项目的说明给出起点路径
    /// </summary>
    [Fact]
    public void 引用非源码项目说明带起点路径()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Web", 7, "src/Web/Web.csproj"), new ProjectNode("Core.Tests", ProjectArea.Test, null)],
            [new ProjectEdge("Web", "Core.Tests")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Contains("Web（framework/src/Web/Web.csproj）", violation.Message);
    }

    /// <summary>
    /// 项目带相对路径时契约包引用实现包的说明给出起点路径
    /// </summary>
    [Fact]
    public void 契约包引用实现包说明带起点路径()
    {
        var graph = new ProjectDependencyGraph(
            [Source("EventBus.Abstractions", 6, "src/EventBus.Abstractions/EventBus.Abstractions.csproj"), Source("EventBus", 6)],
            [new ProjectEdge("EventBus.Abstractions", "EventBus")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Contains("EventBus.Abstractions（framework/src/EventBus.Abstractions/EventBus.Abstractions.csproj）", violation.Message);
    }

    /// <summary>
    /// 项目带相对路径时引用未知项目的说明给出起点路径
    /// </summary>
    [Fact]
    public void 引用未知项目说明带起点路径()
    {
        var graph = new ProjectDependencyGraph(
            [Source("Broken", 3, "src/Broken/Broken.csproj")],
            [new ProjectEdge("Broken", "Missing")]);

        var violation = Assert.Single(ProjectDependencyGraphValidator.Validate(graph, NoExceptions));

        Assert.Equal(ArchitectureViolationKind.UnknownProject, violation.Kind);
        Assert.Contains("Broken（framework/src/Broken/Broken.csproj）", violation.Message);
    }

    private static ProjectNode Source(string name, int rank, string? relativePath = null)
    {
        return new ProjectNode(name, ProjectArea.Source, new ProjectLayer(rank, $"Layer{rank}"), relativePath);
    }
}
