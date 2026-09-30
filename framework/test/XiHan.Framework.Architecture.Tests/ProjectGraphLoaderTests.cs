// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Architecture.Tests.ProjectGraph;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 依赖图读取测试
/// </summary>
public class ProjectGraphLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"xihan_arch_{Guid.NewGuid():N}");

    /// <summary>
    /// 构造函数，在临时目录写出一个最小的仓库结构
    /// </summary>
    public ProjectGraphLoaderTests()
    {
        Write(ProjectGraphLoader.SolutionFileName,
            """
            <Solution>
              <Folder Name="/1.src/1.Common/">
                <Project Path="src/Utils/Utils.csproj" />
                <Project Path="src/Analyzers/Analyzers.csproj" />
              </Folder>
              <Folder Name="/1.src/3.Core/">
                <Project Path="src/Core/Core.csproj" />
              </Folder>
              <Folder Name="/2.tests/1.UnitTests/">
                <Project Path="test/Core.Tests/Core.Tests.csproj" />
              </Folder>
            </Solution>
            """);
        Write("src/Analyzers/Analyzers.csproj", Project());
        Write("src/Utils/Utils.csproj", Project("""<ProjectReference Include="..\Analyzers\Analyzers.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />"""));
        Write("src/Core/Core.csproj", Project("""<ProjectReference Include="..\Utils\Utils.csproj" />"""));
        Write("src/Orphan/Orphan.csproj", Project());
        Write("test/Core.Tests/Core.Tests.csproj", Project("""<ProjectReference Include="..\..\src\Core\Core.csproj" />"""));
    }

    /// <summary>
    /// 分层取自 slnx 目录，分析器引用不计入依赖边
    /// </summary>
    [Fact]
    public void 读取分层与引用且不计入分析器引用()
    {
        var graph = ProjectGraphLoader.Load(_root);

        Assert.Equal(new ProjectLayer(3, "Core"), graph.Projects["Core"].Layer);
        Assert.Equal(new ProjectLayer(1, "Common"), graph.Projects["Utils"].Layer);
        Assert.Equal(ProjectArea.Test, graph.Projects["Core.Tests"].Area);
        Assert.Null(graph.Projects["Core.Tests"].Layer);
        Assert.Contains(new ProjectEdge("Core", "Utils"), graph.Edges);
        Assert.Contains(new ProjectEdge("Core.Tests", "Core"), graph.Edges);
        Assert.DoesNotContain(new ProjectEdge("Utils", "Analyzers"), graph.Edges);
    }

    /// <summary>
    /// src 下未登记到 slnx 的项目也会读入，且没有分层
    /// </summary>
    [Fact]
    public void 未登记到解决方案的源码项目没有分层()
    {
        var graph = ProjectGraphLoader.Load(_root);

        var orphan = graph.Projects["Orphan"];
        Assert.Equal(ProjectArea.Source, orphan.Area);
        Assert.Null(orphan.Layer);
    }

    /// <summary>
    /// 引用的 csproj 不存在时只产生依赖边，由校验器报告为未知项目
    /// </summary>
    [Fact]
    public void 引用不存在的项目只加边不加节点()
    {
        Write("src/Broken/Broken.csproj", Project("""<ProjectReference Include="..\Missing\Missing.csproj" />"""));

        var graph = ProjectGraphLoader.Load(_root);

        Assert.Contains(new ProjectEdge("Broken", "Missing"), graph.Edges);
        Assert.DoesNotContain("Missing", graph.Projects.Keys);

        var violations = ProjectDependencyGraphValidator.Validate(graph, new DependencyPolicy([]));
        Assert.Contains(violations, item => item.Kind == ArchitectureViolationKind.UnknownProject && item.Message.Contains("Missing"));
        Assert.DoesNotContain(violations, item => item.Kind == ArchitectureViolationKind.UnregisteredLayer && item.Message.Contains("Missing"));
    }

    /// <summary>
    /// 引用路径大小写与实际文件不同时，依赖边仍使用实际项目名
    /// </summary>
    [Fact]
    public void 引用路径大小写不同时边使用实际项目名()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(), "仅在文件系统不区分大小写时有意义");

        Write("src/Core/Core.csproj", Project("""<ProjectReference Include="..\utils\UTILS.csproj" />"""));

        var graph = ProjectGraphLoader.Load(_root);

        Assert.Contains(new ProjectEdge("Core", "Utils"), graph.Edges);
        Assert.DoesNotContain(graph.Edges, item => item.To == "UTILS");
    }

    /// <summary>
    /// 以子元素声明 OutputItemType 的分析器引用同样不计入依赖边
    /// </summary>
    [Fact]
    public void 子元素形式的分析器引用不计入依赖边()
    {
        Write("src/Utils/Utils.csproj", Project("""
            <ProjectReference Include="..\Analyzers\Analyzers.csproj">
              <OutputItemType>Analyzer</OutputItemType>
            </ProjectReference>
            """));

        var graph = ProjectGraphLoader.Load(_root);

        Assert.DoesNotContain(new ProjectEdge("Utils", "Analyzers"), graph.Edges);
    }

    /// <summary>
    /// 节点记录相对 framework 目录的路径，向上引用的说明带起点路径
    /// </summary>
    [Fact]
    public void 节点带相对路径且违规说明带起点路径()
    {
        Write(ProjectGraphLoader.SolutionFileName,
            """
            <Solution>
              <Folder Name="/1.src/1.Common/">
                <Project Path="src/Utils/Utils.csproj" />
              </Folder>
              <Folder Name="/1.src/3.Core/">
                <Project Path="src/Core/Core.csproj" />
              </Folder>
              <Folder Name="/1.src/7.Web/">
                <Project Path="src/Web/Web.csproj" />
              </Folder>
            </Solution>
            """);
        Write("src/Web/Web.csproj", Project());
        Write("src/Core/Core.csproj", Project("""<ProjectReference Include="..\Web\Web.csproj" />"""));

        var graph = ProjectGraphLoader.Load(_root);

        Assert.Equal("src/Core/Core.csproj", graph.Projects["Core"].RelativePath);
        var violations = ProjectDependencyGraphValidator.Validate(graph, new DependencyPolicy([]));
        var upward = Assert.Single(violations, item => item.Kind == ArchitectureViolationKind.UpwardReference);
        Assert.Contains("src/Core/Core.csproj", upward.Message);
    }

    /// <summary>
    /// 删除临时目录
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static string Project(string references = "")
    {
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                {references}
              </ItemGroup>
            </Project>
            """;
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
