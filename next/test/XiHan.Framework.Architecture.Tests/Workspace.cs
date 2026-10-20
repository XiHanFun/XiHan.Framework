// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Xml;
using System.Xml.Linq;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 定位 next 工作区并枚举正式包。所有架构测试都以文件系统为事实源，不依赖已编译的程序集。
/// </summary>
internal static class Workspace
{
    public const string PackagePrefix = "XiHan.Framework.";

    public static string Root { get; } = Locate();

    public static string SourceRoot => Path.Combine(Root, "src");

    public static string TestRoot => Path.Combine(Root, "test");

    public static string SpecRoot => Path.Combine(Root, "spec");

    public static IReadOnlyList<SourceProject> SourceProjects { get; } = LoadSourceProjects();

    public static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Where(static f => !IsBuildOutput(f));

    public static XDocument LoadXml(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 XiHan.Framework.Next.slnx，架构测试必须在 next 工作区内运行。");
    }

    private static List<SourceProject> LoadSourceProjects()
    {
        var projects = new List<SourceProject>();
        foreach (var directory in Directory.EnumerateDirectories(SourceRoot))
        {
            var csproj = Path.Combine(directory, Path.GetFileName(directory) + ".csproj");
            if (File.Exists(csproj))
            {
                projects.Add(SourceProject.Load(csproj));
            }
        }

        projects.Sort(static (a, b) => string.CompareOrdinal(a.PackageId, b.PackageId));
        return projects;
    }

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }
}

/// <summary>
/// 一个正式包的项目文件事实：包 ID、项目引用与包引用。
/// </summary>
internal sealed record SourceProject(
    string PackageId,
    string Directory,
    string ProjectPath,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<PackageReference> PackageReferences)
{
    public static SourceProject Load(string csprojPath)
    {
        var document = Workspace.LoadXml(csprojPath);
        var packageId = document.Descendants("PackageId").FirstOrDefault()?.Value
            ?? Path.GetFileNameWithoutExtension(csprojPath);

        var projectReferences = document.Descendants("ProjectReference")
            .Select(static e => Path.GetFileNameWithoutExtension((string?)e.Attribute("Include") ?? string.Empty))
            .ToList();

        var packageReferences = document.Descendants("PackageReference")
            .Select(static e => new PackageReference(
                (string?)e.Attribute("Include") ?? string.Empty,
                string.Equals((string?)e.Attribute("PrivateAssets") ?? e.Element("PrivateAssets")?.Value, "all", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new SourceProject(
            packageId,
            Path.GetDirectoryName(csprojPath)!,
            csprojPath,
            projectReferences,
            packageReferences);
    }
}

internal sealed record PackageReference(string Id, bool IsPrivate);
