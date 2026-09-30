// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XiHan.Framework.Architecture.Tests.ProjectGraph;

/// <summary>
/// 从 slnx 与 csproj 读取项目依赖图
/// </summary>
internal static partial class ProjectGraphLoader
{
    /// <summary>
    /// 解决方案文件名
    /// </summary>
    public const string SolutionFileName = "XiHan.Framework.slnx";

    /// <summary>
    /// 从测试程序所在目录向上查找含解决方案文件的 framework 目录
    /// </summary>
    /// <returns>framework 目录的完整路径</returns>
    /// <exception cref="InvalidOperationException">向上查找到根目录仍未找到解决方案文件</exception>
    public static string FindFrameworkDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            var nested = Path.Combine(directory.FullName, "framework");
            if (File.Exists(Path.Combine(nested, SolutionFileName)))
            {
                return nested;
            }
        }

        throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 向上未找到 {SolutionFileName}。");
    }

    /// <summary>
    /// 读取依赖图
    /// </summary>
    /// <remarks>
    /// 项目集合为 slnx 登记的项目、src 下的全部 csproj 以及它们递归引用到的项目；
    /// 分层取 slnx 的 /1.src/&lt;序号&gt;.&lt;层名&gt;/ 目录；OutputItemType 为 Analyzer 的项目引用不计入依赖边；
    /// 引用的 csproj 不存在时只产生依赖边、不产生节点；项目名取文件系统中的实际文件名；节点记录 csproj 相对 framework 目录的路径；
    /// 不求值 Condition，不读取 props/targets 中的 ProjectReference。
    /// </remarks>
    /// <param name="frameworkDirectory">framework 目录</param>
    /// <returns>项目依赖图</returns>
    public static ProjectDependencyGraph Load(string frameworkDirectory)
    {
        var root = Path.GetFullPath(frameworkDirectory);
        var layers = new Dictionary<string, ProjectLayer?>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var solution = XDocument.Load(Path.Combine(root, SolutionFileName));
        foreach (var project in solution.Descendants("Project"))
        {
            var relativePath = (string?)project.Attribute("Path");
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var folderName = (string?)project.Parent?.Attribute("Name");
            layers[Register(names, ToFullPath(root, relativePath))] = ParseLayer(folderName);
        }

        var sourceDirectory = Path.Combine(root, "src");
        if (Directory.Exists(sourceDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories))
            {
                layers.TryAdd(Register(names, Path.GetFullPath(file)), null);
            }
        }

        var edges = new List<ProjectEdge>();
        var pending = new Queue<string>(layers.Keys);
        while (pending.Count > 0)
        {
            var file = pending.Dequeue();
            if (!File.Exists(file))
            {
                continue;
            }

            var from = names[file];
            var directory = Path.GetDirectoryName(file) ?? root;
            var references = XDocument.Load(file)
                .Descendants()
                .Where(element => element.Name.LocalName == "ProjectReference");

            foreach (var reference in references)
            {
                var include = (string?)reference.Attribute("Include");
                if (string.IsNullOrWhiteSpace(include) || IsAnalyzerReference(reference))
                {
                    continue;
                }

                var target = ToFullPath(directory, include);
                if (!File.Exists(target))
                {
                    edges.Add(new ProjectEdge(from, Path.GetFileNameWithoutExtension(target)));
                    continue;
                }

                var key = Register(names, target);
                if (layers.TryAdd(key, null))
                {
                    pending.Enqueue(key);
                }

                edges.Add(new ProjectEdge(from, names[key]));
            }
        }

        var nodes = layers.Select(pair => new ProjectNode(
            names[pair.Key],
            GetArea(root, pair.Key),
            pair.Value,
            Path.GetRelativePath(root, pair.Key).Replace(Path.DirectorySeparatorChar, '/')));

        return new ProjectDependencyGraph(nodes, edges);
    }

    [GeneratedRegex(@"^/1\.src/(?<rank>\d+)\.(?<name>[^/]+)/$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceLayerFolder();

    private static ProjectLayer? ParseLayer(string? folderName)
    {
        if (string.IsNullOrEmpty(folderName))
        {
            return null;
        }

        var match = SourceLayerFolder().Match(folderName);
        if (!match.Success)
        {
            return null;
        }

        return new ProjectLayer(
            int.Parse(match.Groups["rank"].Value, System.Globalization.CultureInfo.InvariantCulture),
            match.Groups["name"].Value);
    }

    private static string Register(Dictionary<string, string> names, string path)
    {
        var resolved = ResolveActualPath(path);
        names.TryAdd(resolved, Path.GetFileNameWithoutExtension(resolved));
        return resolved;
    }

    private static string ResolveActualPath(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is null || !Directory.Exists(directory))
        {
            return path;
        }

        var fileName = Path.GetFileName(path);
        var actual = Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .Where(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => string.Equals(name, fileName, StringComparison.Ordinal) ? 0 : 1)
            .FirstOrDefault();

        return actual is null ? path : Path.Combine(directory, actual);
    }

    private static bool IsAnalyzerReference(XElement reference)
    {
        var outputItemType = (string?)reference.Attribute("OutputItemType")
            ?? reference.Elements().FirstOrDefault(element => element.Name.LocalName == "OutputItemType")?.Value;

        return string.Equals(outputItemType?.Trim(), "Analyzer", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToFullPath(string baseDirectory, string relativePath)
    {
        var normalized = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(baseDirectory, normalized));
    }

    private static ProjectArea GetArea(string root, string file)
    {
        var relative = Path.GetRelativePath(root, file);
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

        return firstSegment switch
        {
            "src" => ProjectArea.Source,
            "test" => ProjectArea.Test,
            "sample" => ProjectArea.Sample,
            "tool" => ProjectArea.Tool,
            _ => ProjectArea.Other
        };
    }
}
